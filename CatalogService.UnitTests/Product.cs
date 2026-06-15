using CatalogService.Application.Handlers;
using CatalogService.Data;
using CatalogService.Data.Repositories;
using CatalogService.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CatalogService.UnitTests
{
    public class ProductCommandHandlerTests
    {
        private static (ProductRepository repo, IDistributedCache cache) BuildRepoAndCache(string dbName)
        {
            var options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            var ctx = new DataContext(options);
            var repo = new ProductRepository(ctx);

            var memOptions = Options.Create(new MemoryDistributedCacheOptions());
            IDistributedCache cache = new MemoryDistributedCache(memOptions);
            return (repo, cache);
        }

        [Fact]
        public async Task AddProduct_Should_Create_Product_And_Bump_List_Version()
        {
            var (repo, cache) = BuildRepoAndCache(nameof(AddProduct_Should_Create_Product_And_Bump_List_Version));
            var handler = new AddProductCommandHandler(repo, cache);
            var cmd = new AddProductCommand
            {
                Name = "Test Product",
                Description = "Desc",
                Price = 10.5m,
                Stock = 5,
                IsActive = true
            };

            var result = await handler.Handle(cmd, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBe(Guid.Empty);

            var created = await repo.GetByIdAsync(result.Value, CancellationToken.None);
            created.Should().NotBeNull();
            created!.Name.Should().Be("Test Product");

            var versionBytes = await cache.GetAsync("products:list:version");
            var versionStr = versionBytes is null ? null : System.Text.Encoding.UTF8.GetString(versionBytes);
            versionStr.Should().NotBeNull();
            int.TryParse(versionStr, out var version).Should().BeTrue();
            version.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateProduct_Should_Update_Fields_Remove_ItemCache_And_Bump_List_Version()
        {
            var (repo, cache) = BuildRepoAndCache(nameof(UpdateProduct_Should_Update_Fields_Remove_ItemCache_And_Bump_List_Version));

            var product = new Product
            {
                Name = "Old",
                Description = "OldDesc",
                Price = 1,
                Stock = 1,
                IsActive = true
            };
            await repo.AddAsync(product, CancellationToken.None);
            await repo.UnitOfWork.SaveChangesAsync(CancellationToken.None);

            var productKey = $"product:{product.Id}";
            await cache.SetAsync(productKey, System.Text.Encoding.UTF8.GetBytes("cached"), new DistributedCacheEntryOptions());

            var handler = new UpdateProductCommandHandler(repo, cache);
            var cmd = new UpdateProductCommand
            {
                Id = product.Id,
                Name = "New",
                Description = "NewDesc",
                Price = 2,
                Stock = 3,
                IsActive = false
            };

            var result = await handler.Handle(cmd, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(product.Id);

            var updated = await repo.GetByIdAsync(product.Id, CancellationToken.None);
            updated!.Name.Should().Be("New");
            updated.Description.Should().Be("NewDesc");
            updated.Price.Should().Be(2);
            updated.Stock.Should().Be(3);
            updated.IsActive.Should().BeFalse();

            var cachedAfter = await cache.GetAsync(productKey);
            cachedAfter.Should().BeNull();

            var versionBytes = await cache.GetAsync("products:list:version");
            var versionStr = versionBytes is null ? null : System.Text.Encoding.UTF8.GetString(versionBytes);
            versionStr.Should().NotBeNull();
            int.TryParse(versionStr, out var version).Should().BeTrue();
            version.Should().BeGreaterThan(0);
        }
    }
}
