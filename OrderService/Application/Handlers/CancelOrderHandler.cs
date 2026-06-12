
using Shared.Application.MediatR;
using Shared.Application.Result;
using OrderService.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Http;
using Shared.Contracts.Catalog.Dtos;


namespace OrderService.Application.Handlers
{
    public class CancelOrderCommand : ICommand<Result>
    {
        public Guid Id { get; set; }
    }

    public class CancelOrderCommandHandler(OrderRepository repository, ICatalogServiceClient catalogServiceClient) : ICommandHandler<CancelOrderCommand, Result>
    {
        private readonly OrderRepository _repository = repository;
        private readonly ICatalogServiceClient _catalogServiceClient = catalogServiceClient;

        public async Task<Result> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
        {
            if (request == null || request.Id == Guid.Empty)
                return new Result { IsSuccess = false, ErrorMessage = "Invalid request" };

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
            }
            catch (Exception ex)
            {
                return new Result { IsSuccess = false, ErrorMessage = "Error cancelling order: " + ex.Message };
            }

            return new Result { IsSuccess = true, ErrorMessage = string.Empty };
        }
    }
}
