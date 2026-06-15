
using Shared.Application.MediatR;
using Shared.Application.Result;
using OrderService.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Catalog.Dtos;
using Shared.Infrastructure.Http;


namespace OrderService.Application.Handlers
{
    public class CancelOrderCommand : ICommand<Result>
    {
        public Guid Id { get; set; }
    }

    public class CancelOrderCommandHandler(OrderRepository repository, ICatalogServiceClient catalogServiceClient, Shared.Infrastructure.Redis.IIdempotencyStore idempotencyStore) : ICommandHandler<CancelOrderCommand, Result>
    {
        private readonly OrderRepository _repository = repository;
        private readonly ICatalogServiceClient _catalogServiceClient = catalogServiceClient;
        private readonly Shared.Infrastructure.Redis.IIdempotencyStore _idempotencyStore = idempotencyStore;

        public async Task<Result> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
        {
            if (request == null || request.Id == Guid.Empty)
                return new Result { IsSuccess = false, ErrorMessage = "Invalid request" };

            // Idempotency: ensure cancel is applied at most once per order id
            var idemKey = $"order:cancel:{request.Id}";
            var started = await _idempotencyStore.TryStartProcessingAsync(idemKey, TimeSpan.FromMinutes(2));
            if (!started)
            {
                var (found, val) = await _idempotencyStore.GetAsync(idemKey);
                if (found && string.Equals(val, "done", StringComparison.OrdinalIgnoreCase))
                {
                    return new Result { IsSuccess = true, ErrorMessage = string.Empty };
                }
                // Already processing or unknown state — be conservative and return success without reapplying
                return new Result { IsSuccess = true, ErrorMessage = string.Empty };
            }

            var dbSet = _repository.GetDbSet();
            var order = await dbSet.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
            if (order == null) return new Result { IsSuccess = false, ErrorMessage = "Order not found" };

            if (order.Status == Shared.Contracts.Order.Enums.OrderStatus.Cancelled)
                return new Result { IsSuccess = true, ErrorMessage = string.Empty };

            // Update status
            order.Status = Shared.Contracts.Order.Enums.OrderStatus.Cancelled;

            try
            {
                _repository.Update(order);
                await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                // Prepare increase stock items
                var increaseItems = order.Items.Select(i => new IncreaseItemDto { ProductId = i.ProductId, Quantity = i.Quantity }).ToList();
                if (increaseItems.Count > 0)
                {
                    await _catalogServiceClient.IncreaseStock(increaseItems);
                }

                // mark idempotency as completed
                await _idempotencyStore.SetResultAsync(idemKey, "done", TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                await _idempotencyStore.RemoveAsync(idemKey);
                return new Result { IsSuccess = false, ErrorMessage = "Error cancelling order: " + ex.Message };
            }

            return new Result { IsSuccess = true, ErrorMessage = string.Empty };
        }
    }
}
