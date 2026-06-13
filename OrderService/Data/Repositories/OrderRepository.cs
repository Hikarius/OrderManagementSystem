using OrderService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Data;

namespace OrderService.Data.Repositories
{
    public class OrderRepository : EfRepository<Order>, IRepository<Order>
    {
        public OrderRepository(OrderService.Data.DataContext dbContext) : base(dbContext)
        {
        }
    }
}
