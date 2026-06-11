using CatalogService.ReadModel.Dtos;
using CatalogService.ReadModel.Repositories;
using CatalogService.ReadModel.Queries;

namespace CatalogService.ReadModel.Handlers
{
    public class GetProductByIdHandler
    {
        private readonly InMemoryProductReadRepository _repo;

        public GetProductByIdHandler(InMemoryProductReadRepository repo)
        {
            _repo = repo;
        }

        public async Task<ProductDto?> Handle(GetProductByIdQuery query)
        {
            return await _repo.GetByIdAsync(query.Id);
        }
    }
}
