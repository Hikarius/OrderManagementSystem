using Microsoft.Extensions.Caching.Distributed;
using OrderService.Data.Repositories;
using System.Text.Json;
using Shared.Application.Result;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Order;
using System.Text;

namespace OrderService.Application.Queries
{
    public class OrderQueries
    {
        private readonly OrderRepository _repository;
        private readonly IDistributedCache _cache;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public OrderQueries(OrderRepository repository, IDistributedCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<Result<List<OrderDto>>> GetOrders(GetOrdersFilter? filter)
        {
            var dbSet = _repository.GetDbSet().AsQueryable();

            // base query includes items
            IQueryable<Domain.Entities.Order> query = dbSet.Include(o => o.Items);

            if (filter != null)
            {
                // simple status filter
                if (filter.Status.HasValue)
                    query = query.Where(o => o.Status == filter.Status.Value);

                // sorting
                var desc = string.Equals(filter.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);
                switch ((filter.SortBy ?? "createdat").Trim().ToLowerInvariant())
                {
                    case "createdat":
                    case "created":
                        query = desc ? query.OrderByDescending(o => o.CreatedAt) : query.OrderBy(o => o.CreatedAt);
                        break;
                    case "updated":
                    case "updatedat":
                        query = desc ? query.OrderByDescending(o => o.UpdatedAt) : query.OrderBy(o => o.UpdatedAt);
                        break;
                    default:
                        query = query.OrderBy(o => o.Id);
                        break;
                }

                if (filter.PageSize > 0)
                {
                    var page = Math.Max(filter.PageNumber, 1);
                    var size = filter.PageSize;
                    query = query.Skip((page - 1) * size).Take(size);
                }
            }
            else
            {
                query = query.OrderBy(o => o.Id);
            }

            var orders = await query.Select(o => new OrderDto
            {
                Id = o.Id,
                CustomerEmail = o.CustomerEmail,
                CustomerTelNumber = o.CustomerTelNumber,
                TotalPrice = o.TotalPrice,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                IsDeleted = false,
                Items = o.Items.Select(it => new OrderItemDto
                {
                    Id = it.Id,
                    ProductId = it.ProductId,
                    ProductName = it.ProductName,
                    Quantity = it.Quantity,
                    UnitPrice = it.UnitPrice,
                    TotalPrice = it.TotalPrice
                }).ToList()
            }).ToListAsync();

            return new Result<List<OrderDto>> { IsSuccess = true, ErrorMessage = string.Empty, Value = orders };
        }

        public async Task<int> GetOrdersTotalCount(GetOrdersFilter? filter)
        {
            var dbSet = _repository.GetDbSet().AsQueryable();
            IQueryable<Domain.Entities.Order> query = dbSet;
            if (filter != null && filter.Status.HasValue)
            {
                query = query.Where(o => o.Status == filter.Status.Value);
            }
            return await query.CountAsync();
        }

        public async Task<Result<OrderDto>> GetOrderById(Guid id)
        {
            if (id == Guid.Empty) return new Result<OrderDto> { IsSuccess = false, ErrorMessage = "Invalid id", Value = null };

            var cacheKey = $"order:{id}";
            var cached = await _cache.GetAsync(cacheKey);
            if (cached is not null)
            {
                var dto = JsonSerializer.Deserialize<OrderDto>(Encoding.UTF8.GetString(cached), _jsonOptions);
                if (dto is not null)
                    return new Result<OrderDto> { IsSuccess = true, ErrorMessage = string.Empty, Value = dto };
            }

            var dbSet = _repository.GetDbSet();
            var order = await dbSet.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return new Result<OrderDto> { IsSuccess = false, ErrorMessage = "Order not found", Value = null };

            var orderDto = new OrderDto
            {
                Id = order.Id,
                CustomerEmail = order.CustomerEmail,
                CustomerTelNumber = order.CustomerTelNumber,
                TotalPrice = order.TotalPrice,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                IsDeleted = false,
                Items = order.Items.Select(it => new OrderItemDto
                {
                    Id = it.Id,
                    ProductId = it.ProductId,
                    ProductName = it.ProductName,
                    Quantity = it.Quantity,
                    UnitPrice = it.UnitPrice,
                    TotalPrice = it.TotalPrice
                }).ToList()
            };

            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderDto, _jsonOptions));
            await _cache.SetAsync(cacheKey, payload, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

            return new Result<OrderDto> { IsSuccess = true, ErrorMessage = string.Empty, Value = orderDto };
        }

        public class GetOrdersFilter
        {
            public int PageSize { get; set; } = 10;
            public int PageNumber { get; set; } = 1;
            public string? SortBy { get; set; }
            public string? SortOrder { get; set; }
            public Shared.Contracts.Order.Enums.OrderStatus? Status { get; set; }
        }
    }
}
