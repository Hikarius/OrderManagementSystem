using System.Collections.Concurrent;
using CatalogService.ReadModel.Dtos;

namespace CatalogService.ReadModel.Repositories
{
    public class InMemoryProductReadRepository
    {
        private readonly ConcurrentDictionary<Guid, ProductDto> _store = new();

        public InMemoryProductReadRepository()
        {
            // seed sample data
            var id = Guid.NewGuid();
            _store[id] = new ProductDto { Id = id, Name = "Sample Product", Description = "A sample product", Price = 9.99m };
        }

        public Task<ProductDto?> GetByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var dto);
            return Task.FromResult(dto);
        }
    }
}
