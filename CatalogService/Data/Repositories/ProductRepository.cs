using CatalogService.Domain.Entities;
using Shared.Infrastructure.Data;

namespace CatalogService.Data.Repositories
{
    public class ProductRepository(DataContext dbContext) : EfRepository<Product>(dbContext)
    {
    }
}
