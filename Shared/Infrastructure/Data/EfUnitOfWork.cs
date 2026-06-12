using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Shared.Infrastructure.Data
{
    // Minimal EF Core-backed UnitOfWork. Consider moving this implementation to projects that reference EF Core.
    public class EfUnitOfWork : IUnitOfWork
    {
        private readonly DbContext _dbContext;
        private IDbContextTransaction? _currentTransaction;

        public EfUnitOfWork(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction is not null)
                return;

            _currentTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction is null)
                return;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                if (entry.Entity is IAggregateRoot)
                {
                    if (entry.State == EntityState.Added)
                    {
                        if (entry.Entity is AggregateRoot ar)
                        {
                            ar.CreatedAt = now;
                            ar.UpdatedAt = null;
                        }
                        else
                        {
                            if (entry.Metadata.FindProperty("CreatedAt") is not null)
                                entry.Property("CreatedAt").CurrentValue = now;
                            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                                entry.Property("UpdatedAt").CurrentValue = null;
                        }
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        if (entry.Entity is AggregateRoot ar)
                        {
                            ar.UpdatedAt = now;
                            // ensure CreatedAt isn't flagged as modified
                            entry.Property(nameof(ar.CreatedAt)).IsModified = false;
                        }
                        else
                        {
                            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                                entry.Property("UpdatedAt").CurrentValue = now;
                            if (entry.Metadata.FindProperty("CreatedAt") is not null)
                                entry.Property("CreatedAt").IsModified = false;
                        }
                    }
                }
            }

            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction is null)
                return;

            await _currentTransaction.RollbackAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
