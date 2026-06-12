using CatalogService.Data.Repositories;
using CatalogService.Domain.Entities;
using Shared.Application.MediatR;
using Shared.Application.Result;

namespace CatalogService.Application.Handlers
{

    public class AddProductCommand : ICommand<Result<Guid>>
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class AddProductCommandHandler : ICommandHandler<AddProductCommand, Result<Guid>>
    {
        private readonly ProductRepository repository;
        private readonly Microsoft.Extensions.Caching.Distributed.IDistributedCache _cache;

        public AddProductCommandHandler(ProductRepository repository, Microsoft.Extensions.Caching.Distributed.IDistributedCache cache) => (this.repository, _cache) = (repository, cache);

        public async Task<Result<Guid>> Handle(AddProductCommand request, CancellationToken cancellationToken)
        {
            if (request is null) return new Result<Guid> { IsSuccess = false, ErrorMessage = "Request cannot be null."};

            var newProduct = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Stock = request.Stock,
                IsActive = request.IsActive
            };
            try
            {
                await repository.AddAsync(newProduct, cancellationToken);
                // persist
                await repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                // Invalidate list caches by bumping version
                var versionKey = "products:list:version";
                var versionBytes = await _cache.GetAsync(versionKey);
                var version = versionBytes is null ? "1" : System.Text.Encoding.UTF8.GetString(versionBytes);
                if (int.TryParse(version, out var v)) v++; else v = 1;
                await _cache.SetAsync(versionKey, System.Text.Encoding.UTF8.GetBytes(v.ToString()), new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions());
            }
            catch(Exception e)
            {
                return new Result<Guid> { IsSuccess = false, ErrorMessage = "An error occurred while adding the product: " + e.Message };
            }

            return new Result<Guid> { IsSuccess = true, ErrorMessage = "Product added successfully.", Value = newProduct.Id};

        }
    }
}
