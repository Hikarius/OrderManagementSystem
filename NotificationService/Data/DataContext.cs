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
                builder.HasKey(n => n.OrderId);
                builder.Property(n => n.Channel).IsRequired().HasMaxLength(50);
                builder.Property(n => n.Message).IsRequired();
            });
        }
    }
}
