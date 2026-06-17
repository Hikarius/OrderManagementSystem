using CatalogService.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Application.MediatR;
using Shared.Application.Result;

namespace CatalogService.Application.Handlers
{
    public class UpdateProductCommand : ICommand<Result<Guid>>
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, Result<Guid>>
    {
        private readonly ProductRepository repository;
        private readonly Microsoft.Extensions.Caching.Distributed.IDistributedCache _cache;

        public UpdateProductCommandHandler(ProductRepository repository, Microsoft.Extensions.Caching.Distributed.IDistributedCache cache) => (this.repository, _cache) = (repository, cache);

        public async Task<Result<Guid>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            if (request is null) return new Result<Guid> { IsSuccess = false, ErrorMessage = "Request cannot be null." };

            var product = await repository.GetByIdAsync(request.Id, cancellationToken);
            if (product is null) return new Result<Guid> { IsSuccess = false, ErrorMessage = "Product not found." };

            var existingProductWithSameName = await repository.GetDbSet().FirstOrDefaultAsync(p => p.Name == request.Name && p.Id != request.Id, cancellationToken);
            if(existingProductWithSameName is not null) return new Result<Guid> { IsSuccess = false, ErrorMessage = "A product with the same name already exists." };

            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.Stock = request.Stock;
            product.IsActive = request.IsActive;

            try
            {
                repository.Update(product);
                await repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                // Invalidate product cache and bump list version
                var productKey = $"product:{product.Id}";
                await _cache.RemoveAsync(productKey);
                var versionKey = "products:list:version";
                var versionBytes = await _cache.GetAsync(versionKey);
                var version = versionBytes is null ? "1" : System.Text.Encoding.UTF8.GetString(versionBytes);
                if (int.TryParse(version, out var v)) v++; else v = 1;
                await _cache.SetAsync(versionKey, System.Text.Encoding.UTF8.GetBytes(v.ToString()), new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions());
            }
            catch (Exception e)
            {
                return new Result<Guid> { IsSuccess = false, ErrorMessage = "An error occurred while updating the product: " + e.Message };
            }

            return new Result<Guid> { IsSuccess = true, ErrorMessage = "Product updated successfully.", Value = product.Id };
        }
    }
}
