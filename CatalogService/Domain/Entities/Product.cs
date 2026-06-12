using Shared.Infrastructure.Data;

namespace CatalogService.Domain.Entities
{
    public class Product : AggregateRoot
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; } 
        public bool IsActive { get; set; } = true;
        // PostgreSQL xmin rowversion for optimistic concurrency
        public uint Xmin { get; set; }

    }
}
