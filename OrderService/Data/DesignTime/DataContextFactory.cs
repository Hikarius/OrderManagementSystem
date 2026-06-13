using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace OrderService.Data.DesignTime
{
    public class DataContextFactory : IDesignTimeDbContextFactory<OrderService.Data.DataContext>
    {
        public OrderService.Data.DataContext CreateDbContext(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            var conn = configuration.GetConnectionString("DefaultConnection") ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION") ?? string.Empty;

            var options = new DbContextOptionsBuilder<OrderService.Data.DataContext>();
            options.UseNpgsql(conn, b => b.MigrationsAssembly(typeof(OrderService.Data.DataContext).Assembly.FullName));

            return new OrderService.Data.DataContext(options.Options);
        }
    }
}
