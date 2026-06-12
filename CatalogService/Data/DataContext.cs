using CatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data
{
    public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(builder =>
            {
                // map xmin to concurrency token
                builder.Property(p => p.Xmin).HasColumnName("xmin").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
            });
        }
    }
}
