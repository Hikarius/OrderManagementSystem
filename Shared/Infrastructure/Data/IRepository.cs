using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Infrastructure.Data
{
    public interface IRepository<T> where T : class, IAggregateRoot
    {
        IUnitOfWork UnitOfWork { get; }
        Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<T>> ListAsync(int pageSize = 20, int pageNumber = 10,  CancellationToken cancellationToken = default);
        Task AddAsync(T entity, CancellationToken cancellationToken = default);
        void Update(T entity);
        void Remove(T entity);
    }
}
