using CatalogService.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Shared.Application.MediatR;
using Shared.Application.Result;

namespace CatalogService.Application.Handlers
{
    public class IncreaseStockCommand : ICommand<Result<bool>>
    {
        public List<IncreaseItemStock> Items { get; set; } = new List<IncreaseItemStock>();
    }

    public class IncreaseItemStock
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class IncreaseStockHandler(ProductRepository repository, IDistributedCache cache) : ICommandHandler<IncreaseStockCommand, Result<bool>>
    {
        private readonly ProductRepository _repository = repository;
        private readonly IDistributedCache _cache = cache;

        public async Task<Result<bool>> Handle(IncreaseStockCommand request, CancellationToken cancellationToken)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
                return new Result<bool> { IsSuccess = false, ErrorMessage = "Invalid request", Value = false };

            var dbSet = _repository.GetDbSet();
            var ids = request.Items.Select(i => i.ProductId).Distinct().ToList();

            if (ids.Count == 0)
                return new Result<bool> { IsSuccess = false, ErrorMessage = "No product ids provided", Value = false };

            await _repository.UnitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var products = await dbSet.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);

                // Validate existence
                var missing = ids.Except(products.Select(p => p.Id)).ToList();
                if (missing.Count > 0)
                {
                    await _repository.UnitOfWork.RollbackAsync(cancellationToken);
                    return new Result<bool> { IsSuccess = false, ErrorMessage = "One or more products not found", Value = false };
                }

                // Apply increases
                foreach (var item in request.Items)
                {
                    var prod = products.First(p => p.Id == item.ProductId);
                    prod.Stock += item.Quantity;
                    _repository.Update(prod);
                }

                try
                {
                    await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);
                    await _repository.UnitOfWork.CommitAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await _repository.UnitOfWork.RollbackAsync(cancellationToken);
                    return new Result<bool> { IsSuccess = false, ErrorMessage = "Concurrency conflict updating stock. Please retry.", Value = false };
                }

                // Evict caches
                foreach (var id in ids)
                {
                    var cacheKey = $"product:{id}";
                    await _cache.RemoveAsync(cacheKey);
                }

                // bump list version once
                var version = await _cache.GetStringAsync("products:list:version");
                var ver = 1;
                if (!string.IsNullOrWhiteSpace(version) && int.TryParse(version, out var parsed)) ver = parsed + 1; else ver++;
                await _cache.SetStringAsync("products:list:version", ver.ToString());
            }
            catch (Exception ex)
            {
                await _repository.UnitOfWork.RollbackAsync(cancellationToken);
                return new Result<bool> { IsSuccess = false, ErrorMessage = "Error updating stock: " + ex.Message, Value = false };
            }

            return new Result<bool> { IsSuccess = true, ErrorMessage = string.Empty, Value = true };
        }
    }
}
