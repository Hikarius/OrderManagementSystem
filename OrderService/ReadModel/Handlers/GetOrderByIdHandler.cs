using OrderService.ReadModel.Dtos;
using OrderService.ReadModel.Queries;
using Shared.Infrastructure.UnitOfWork;
using OrderService.Domain.Entities;

namespace OrderService.ReadModel.Handlers
{
    public class GetOrderByIdHandler
    {
        private readonly IRepository<Order> _repo;

        public GetOrderByIdHandler(IRepository<Order> repo)
        {
            _repo = repo;
        }

        public async Task<OrderDto?> Handle(GetOrderByIdQuery query)
        {
            var order = await _repo.GetByIdAsync(query.Id);
            if (order == null) return null;

            return new OrderDto
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                CreatedAt = order.CreatedAt,
                Total = order.Total
            };
        }
    }
}
