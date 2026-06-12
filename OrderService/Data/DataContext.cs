using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Data
{
    public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>(builder =>
            {
                builder.ToTable("Orders");
                builder.HasKey(o => o.Id);
                builder.Property(o => o.CustomerEmail).IsRequired().HasMaxLength(256);
                builder.Property(o => o.CustomerTelNumber).HasMaxLength(50);
                builder.Property(o => o.Status).IsRequired();
                builder.Ignore(o => o.TotalPrice);

                builder.HasMany(o => o.Items)
                    .WithOne(i => i.Order)
                    .HasForeignKey(i => i.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OrderItem>(builder =>
            {
                builder.ToTable("OrderItems");
                builder.HasKey(i => i.Id);
                builder.Property(i => i.ProductId).IsRequired();
                builder.Property(i => i.ProductName).IsRequired().HasMaxLength(256);
                builder.Property(i => i.Quantity).IsRequired();
                builder.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
                builder.Ignore(i => i.TotalPrice);
            });
        }
    }
}
