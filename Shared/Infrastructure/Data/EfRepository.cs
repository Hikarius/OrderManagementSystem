using Microsoft.EntityFrameworkCore;

namespace Shared.Infrastructure.Data
{
    public class EfRepository<T> : IRepository<T> where T : class, IAggregateRoot
    {
        private readonly DbContext _dbContext;
        private readonly DbSet<T> _dbSet;

        public EfRepository(DbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = _dbContext.Set<T>();
        }

        public IUnitOfWork UnitOfWork => new EfUnitOfWork(_dbContext);

        public DbSet<T> GetDbSet() => _dbSet;

        public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
        }

        public async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FindAsync(new[] { id }, cancellationToken);
        }

        public async Task<IReadOnlyList<T>> ListAsync(int pageSize = 20, int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            if (pageNumber < 0) pageNumber = 0;

            // If an invalid pageSize is provided, fall back to returning all items.
            if (pageSize <= 0)
            {
                return await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
            }

            var skip = pageNumber * pageSize;
            var query = _dbSet.AsNoTracking().Skip(skip).Take(pageSize);
            return await query.ToListAsync(cancellationToken);
        }

        public void Remove(T entity)
        {
            _dbSet.Remove(entity);
        }

        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }
    }
}
