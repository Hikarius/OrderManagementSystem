using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.UnitOfWork;
using OrderService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace OrderService.ReadModel.Repositories
{
    public class OrderReadRepository : EfRepository<Order>, IRepository<Order>
    {
        public OrderReadRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
