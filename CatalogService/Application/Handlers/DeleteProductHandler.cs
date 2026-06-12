using CatalogService.Data.Repositories;
using Shared.Application.MediatR;
using Shared.Application.Result;

namespace CatalogService.Application.Handlers
{
    public class DeleteProductCommand : ICommand<Result<Guid>>
    {
        public Guid Id { get; set; }
        public DeleteProductCommand() { Id = Guid.Empty; }
        public DeleteProductCommand(Guid id) => Id = id;
    }

    public class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand, Result<Guid>>
    {
        private readonly ProductRepository repository;
        private readonly Microsoft.Extensions.Caching.Distributed.IDistributedCache _cache;

        public DeleteProductCommandHandler(ProductRepository repository, Microsoft.Extensions.Caching.Distributed.IDistributedCache cache) => (this.repository, _cache) = (repository, cache);

        public async Task<Result<Guid>> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
        {
            if (request is null) return new Result<Guid> { IsSuccess = false, ErrorMessage = "Request cannot be null." };

            var product = await repository.GetByIdAsync(request.Id, cancellationToken);
            if (product is null) return new Result<Guid> { IsSuccess = false, ErrorMessage = "Product not found." };

            try
            {
                product.IsDeleted = true;
                repository.Update(product);
                await repository.UnitOfWork.SaveChangesAsync(cancellationToken);

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
                return new Result<Guid> { IsSuccess = false, ErrorMessage = "An error occurred while deleting the product: " + e.Message };
            }

            return new Result<Guid> { IsSuccess = true, ErrorMessage = "Product deleted successfully.", Value = request.Id };
        }
    }
}
