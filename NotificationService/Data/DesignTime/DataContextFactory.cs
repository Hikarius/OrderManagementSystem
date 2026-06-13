using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace NotificationService.Data.DesignTime
{
    public class DataContextFactory : IDesignTimeDbContextFactory<NotificationService.Data.DataContext>
    {
        public NotificationService.Data.DataContext CreateDbContext(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            var conn = configuration.GetConnectionString("DefaultConnection") ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION") ?? string.Empty;

            var options = new DbContextOptionsBuilder<NotificationService.Data.DataContext>();
            options.UseNpgsql(conn, b => b.MigrationsAssembly(typeof(NotificationService.Data.DataContext).Assembly.FullName));

            return new NotificationService.Data.DataContext(options.Options);
        }
    }
}
