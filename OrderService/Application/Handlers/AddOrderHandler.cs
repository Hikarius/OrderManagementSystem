using OrderService.Data.Repositories;
using OrderService.Domain.Entities;
using Shared.Application.MediatR;
using Shared.Application.Messaging;
using Shared.Application.Result;
using Shared.Contracts.Catalog.Dtos;
using Shared.Contracts.Order;
using Shared.Infrastructure.Http;
using Shared.Infrastructure.Redis;
using static Shared.Contracts.Order.Enums;

namespace OrderService.Application.Handlers
{
    public class AddOrderCommand :ICommand<Result<Guid>>
    {
        public required string CustomerEmail { get; set; }
        public string? CustomerTelNumber { get; set; }
        public List<AddOrderItemCommand> Items { get; set; } = [];
        public string? IdempotencyKey { get; set; }
    }
    public class AddOrderItemCommand
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }
    
    public class AddOrderCommandHandler(OrderRepository repository, ICatalogServiceClient catalogServiceClient, IIdempotencyStore idempotencyStore, IEventPublisher eventPublisher) : ICommandHandler<AddOrderCommand, Result<Guid>>
    {
        private readonly OrderRepository _repository = repository;
        private readonly ICatalogServiceClient _catalogServiceClient = catalogServiceClient;
        private readonly IIdempotencyStore _idempotencyStore = idempotencyStore;
        private readonly IEventPublisher _eventPublisher = eventPublisher;

        public async Task<Result<Guid>> Handle(AddOrderCommand request, CancellationToken cancellationToken)
        {
            if (request is null) return new Result<Guid> { IsSuccess = false, ErrorMessage = "Request cannot be null." };

            var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

            // Idempotency handling using Redis store
            var idemKey = request.IdempotencyKey;
            if (!string.IsNullOrWhiteSpace(idemKey))
            {
                // Try to mark as processing. If it already exists, return stored response or a processing indicator.
                var started = await _idempotencyStore.TryStartProcessingAsync(idemKey, TimeSpan.FromMinutes(2));
                if (!started)
                {
                    var (found, val) = await _idempotencyStore.GetAsync(idemKey);
                    if (found)
                    {
                        if (val == "processing")
                        {
                            return new Result<Guid> { IsSuccess = false, ErrorMessage = "Request is already being processed" };
                        }

                        try
                        {
                            var existing = System.Text.Json.JsonSerializer.Deserialize<Result<Guid>>(val, jsonOptions);
                            if (existing is not null) return existing;
                        }
                        catch { }
                    }

                    // If no stored result, try to become the processor
                    started = await _idempotencyStore.TryStartProcessingAsync(idemKey, TimeSpan.FromMinutes(2));
                    if (!started)
                        return new Result<Guid> { IsSuccess = false, ErrorMessage = "Request is already being processed" };
                }
            }

            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var stocksResult = await _catalogServiceClient.GetProductsByIdList(productIds);

            if(!stocksResult.IsSuccess)
            {
                return new Result<Guid> { IsSuccess = false, ErrorMessage = "Failed to retrieve product information: " + stocksResult.ErrorMessage };
            }

            if(stocksResult.Value == null || stocksResult.Value.Count != productIds.Count)
            {
                return new Result<Guid> { IsSuccess = false, ErrorMessage = "Some products were not found in the catalog." };
            }

            var stocks = stocksResult.Value;
            var notEnoughStockProducts = new List<string>();
            foreach (var item in stocks)
            {
                if(item.Stock < request.Items.First(i => i.ProductId == item.Id).Quantity)
                {
                    notEnoughStockProducts.Add(item.Name);
                }
            }

            if(notEnoughStockProducts.Count > 0)
            {
                return new Result<Guid> { IsSuccess = false, ErrorMessage = "Not enough stock for products: " + string.Join(", ", notEnoughStockProducts) };
            }

            var newOrder = new Order
            {
                CustomerEmail = request.CustomerEmail,
                CustomerTelNumber = request.CustomerTelNumber,
                Status = OrderStatus.Pending,
                Items = request.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    ProductName = stocks.First(j => j.Id == i.ProductId).Name,
                    UnitPrice = stocks.First(j => j.Id == i.ProductId).Price,
                    Quantity = i.Quantity
                }).ToList()
            };

            try
            {

                var decreaseItems = request.Items.Select(i => new DecreaseItemDto { ProductId = i.ProductId, Quantity = i.Quantity }).ToList();
                var decResult = await _catalogServiceClient.DecreaseStock(decreaseItems);
                if (!decResult.IsSuccess || decResult.Value == false)
                {
                    return new Result<Guid> { IsSuccess = false, ErrorMessage = "Failed to reserve product stock: " + decResult.ErrorMessage };
                }

                await _repository.AddAsync(newOrder, cancellationToken);
                await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                // publish event (fire-and-forget)
                try
                {
                    var itemsDto = newOrder.Items.Select(i => new OrderItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice
                    }).ToList();

                    var ev = new Shared.Contracts.Messaging.OrderCreatedEvent
                    {
                        OrderId = newOrder.Id,
                        CustomerEmail = newOrder.CustomerEmail,
                        TotalPrice = newOrder.TotalPrice,
                        Items = itemsDto,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _eventPublisher.PublishAsync(ev, cancellationToken);

                    newOrder.Status = OrderStatus.Confirmed;
                    await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    // swallow; publishing failure should not block order creation
                }

                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    var result = new Result<Guid> { IsSuccess = true, ErrorMessage = string.Empty, Value = newOrder.Id };
                    await _idempotencyStore.SetResultAsync(request.IdempotencyKey, result, TimeSpan.FromHours(24));
                }
            }
            catch (Exception e)
            {
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    await _idempotencyStore.RemoveAsync(request.IdempotencyKey);
                }
                return new Result<Guid> { IsSuccess = false, ErrorMessage = "An error occurred while adding the order: " + e.Message};
            }
            return new Result<Guid> { IsSuccess = true, ErrorMessage = "Order added successfully.", Value = newOrder.Id };
        }
    }

}
