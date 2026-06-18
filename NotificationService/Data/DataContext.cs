using Microsoft.EntityFrameworkCore;

namespace NotificationService.Data
{
    public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
    {
        public DbSet<NotificationService.Domain.Entities.Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NotificationService.Domain.Entities.Notification>(builder =>
            {
                builder.ToTable("Notifications");
                // Use Id as primary key so multiple notifications can exist for the same OrderId
                builder.HasKey(n => n.Id);
                builder.Property(n => n.Id).ValueGeneratedOnAdd();
                builder.Property(n => n.OrderId).IsRequired();
                builder.Property(n => n.Channel).IsRequired().HasMaxLength(50);
                builder.Property(n => n.Message).IsRequired();
            });
        }
    }
}
