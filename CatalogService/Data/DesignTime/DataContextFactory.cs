using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace CatalogService.Data.DesignTime
{
    public class DataContextFactory : IDesignTimeDbContextFactory<CatalogService.Data.DataContext>
    {
        public CatalogService.Data.DataContext CreateDbContext(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            var conn = configuration.GetConnectionString("DefaultConnection") ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION") ?? string.Empty;

            var options = new DbContextOptionsBuilder<CatalogService.Data.DataContext>();
            options.UseNpgsql(conn, b => b.MigrationsAssembly(typeof(CatalogService.Data.DataContext).Assembly.FullName));

            return new CatalogService.Data.DataContext(options.Options);
        }
    }
}
