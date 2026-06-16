using CatalogService.Data.Repositories;
using CatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Result;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text;
using Shared.Contracts.Catalog.Dtos;

namespace CatalogService.Application.Queries
{
    public class ProductQueries
    {
        private readonly ProductRepository _repository;
        private readonly IDistributedCache _cache;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public ProductQueries(ProductRepository repository, IDistributedCache cache)
        {
            _repository = repository;
            _cache = cache;
        }


        public async Task<Result<ProductDto>> GetProductById(Guid guid)
        {
            var cacheKey = $"product:{guid}";

            // Try cache first
            var cached = await _cache.GetAsync(cacheKey);
            if (cached is not null)
            {
                var dto = JsonSerializer.Deserialize<ProductDto>(Encoding.UTF8.GetString(cached), _jsonOptions);
                if (dto is not null)
                    return new Result<ProductDto> { IsSuccess = true, ErrorMessage = string.Empty, Value = dto };
            }

            var product = await _repository.GetByIdAsync(guid);
            if (product == null) return new Result<ProductDto> { IsSuccess = false, ErrorMessage = "Product not found", Value = null };

            var productDto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                IsDeleted = product.IsDeleted
            };

            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(productDto, _jsonOptions));
            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            await _cache.SetAsync(cacheKey, payload, cacheOptions);

            return new Result<ProductDto> { IsSuccess = true, ErrorMessage = string.Empty, Value = productDto };
        }

        public async Task<Result<List<ProductDto>>> GetProducts(GetProductsFilter? filter)
        {
            var dbSet = _repository.GetDbSet();
            // Start with the base queryable
            IQueryable<Product> query = dbSet;

            if (filter != null)
            {
                // Price range
                if (filter.MinPrice.HasValue)
                    query = query.Where(p => p.Price >= filter.MinPrice.Value);
                if (filter.MaxPrice.HasValue)
                    query = query.Where(p => p.Price <= filter.MaxPrice.Value);

                if (!string.IsNullOrWhiteSpace(filter.SortBy))
                {
                    var sortBy = filter.SortBy.Trim().ToLowerInvariant();
                    var desc = string.Equals(filter.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

                    switch (sortBy)
                    {
                        case "price":
                            query = desc ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price);
                            break;
                        case "name":
                            query = desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name);
                            break;
                        case "createdat":
                            query = desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt);
                            break;
                        case "created":
                            query = desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt);
                            break;
                        case "updated":
                            query = desc ? query.OrderByDescending(p => p.UpdatedAt) : query.OrderBy(p => p.UpdatedAt);
                            break;
                        case "updatedat":
                            query = desc ? query.OrderByDescending(p => p.UpdatedAt) : query.OrderBy(p => p.UpdatedAt);
                            break;
                        default:
                            query = query.OrderBy(p => p.Id);
                            break;
                    }
                }
                else
                {
                    query = query.OrderBy(p => p.Id);
                }

                // Pagination (optional)
                if (filter.PageSize > 0)
                {
                    var page = Math.Max(filter.PageNumber, 1);
                    var size = filter.PageSize;
                    query = query.Skip((page - 1) * size).Take(size);
                }
            }
            else
            {
                // Ensure deterministic ordering if no filter provided
                query = query.OrderBy(p => p.Id);
            }

            // Build list cache key using a version token
            var version = await _cache.GetStringAsync("products:list:version") ?? "1";
            var filterKey = filter is null ? "default" : $"p{filter.PageNumber}:s{filter.PageSize}:min{filter.MinPrice}:max{filter.MaxPrice}:sort{filter.SortBy}:order{filter.SortOrder}";
            var listCacheKey = $"products:list:{version}:{filterKey}";

            var cachedList = await _cache.GetAsync(listCacheKey);
            if (cachedList is not null)
            {
                var dtos = JsonSerializer.Deserialize<List<ProductDto>>(Encoding.UTF8.GetString(cachedList), _jsonOptions);
                return new Result<List<ProductDto>> { IsSuccess = true, ErrorMessage = string.Empty, Value = dtos ?? new List<ProductDto>() };
            }

            var productDtos = await query.Select(product => new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                IsDeleted = product.IsDeleted
            }).ToListAsync();

            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(productDtos, _jsonOptions));
            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) };
            await _cache.SetAsync(listCacheKey, payload, cacheOptions);

            return new Result<List<ProductDto>> { IsSuccess = true, ErrorMessage = string.Empty, Value = productDtos };
        }

        public async Task<int> GetProductsTotalCount(GetProductsFilter? filter)
        {
            var dbSet = _repository.GetDbSet();
            IQueryable<Product> query = dbSet;
            if (filter != null)
            {
                if (filter.MinPrice.HasValue)
                    query = query.Where(p => p.Price >= filter.MinPrice.Value);
                if (filter.MaxPrice.HasValue)
                    query = query.Where(p => p.Price <= filter.MaxPrice.Value);
            }
            return await query.CountAsync();
        }

        public class GetProductsFilter
        {
            public string? SortBy { get; set; }
            public string? SortOrder { get; set; }
            public int PageSize { get; set; } = 10;
            public int PageNumber { get; set; } = 1;
            public decimal? MinPrice { get; set; }
            public decimal? MaxPrice { get; set; }
        }
    
        public async Task<Result<List<ProductDto>>> GetProductsByIdList(List<Guid> ids)
        {
            if (ids == null || ids.Count == 0)
                return new Result<List<ProductDto>> { IsSuccess = true, ErrorMessage = string.Empty, Value = new List<ProductDto>() };

            var dbSet = _repository.GetDbSet();

            // Try to read each product from cache first
            var resultMap = new Dictionary<Guid, ProductDto>();
            var missingIds = new List<Guid>();

            foreach (var id in ids.Distinct())
            {
                var cacheKey = $"product:{id}";
                var cached = await _cache.GetAsync(cacheKey);
                if (cached is not null)
                {
                    var dto = JsonSerializer.Deserialize<ProductDto>(Encoding.UTF8.GetString(cached), _jsonOptions);
                    if (dto is not null)
                    {
                        resultMap[id] = dto;
                        continue;
                    }
                }

                missingIds.Add(id);
            }

            if (missingIds.Count > 0)
            {
                var products = await dbSet.Where(p => missingIds.Contains(p.Id)).ToListAsync();

                foreach (var product in products)
                {
                    var dto = new ProductDto
                    {
                        Id = product.Id,
                        Name = product.Name,
                        Description = product.Description,
                        Price = product.Price,
                        Stock = product.Stock,
                        IsActive = product.IsActive,
                        CreatedAt = product.CreatedAt,
                        UpdatedAt = product.UpdatedAt,
                        IsDeleted = product.IsDeleted
                    };

                    resultMap[product.Id] = dto;

                    var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto, _jsonOptions));
                    var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
                    var cacheKey = $"product:{product.Id}";
                    await _cache.SetAsync(cacheKey, payload, cacheOptions);
                }
            }

            // Build final list preserving input order and including duplicates if present
            var finalList = new List<ProductDto>();
            foreach (var id in ids)
            {
                if (resultMap.TryGetValue(id, out var dto))
                {
                    finalList.Add(dto);
                }
            }

            return new Result<List<ProductDto>> { IsSuccess = true, ErrorMessage = string.Empty, Value = finalList };
        }
    }

}
