using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderService.Application.Handlers;
using OrderService.Data;
using Shared.Application.Messaging;
using Shared.Application.Result;
using Shared.Contracts.Catalog.Dtos;
using Shared.Infrastructure.Http;
using Shared.Infrastructure.Redis;

namespace OrderService.UnitTests
{
    public class OrderServiceWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            // Ensure Program.cs recognizes test mode before services are added
            builder.UseSetting("DOTNET_RUNNING_IN_TEST", "1");

            builder.ConfigureServices(services =>
            {
                // Set test environment flag
                Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_TEST", "1");

                // Replace DbContext with InMemory
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<DataContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<DataContext>(options => options.UseInMemoryDatabase("OrderServiceTestDb"));

                // Swap DistributedCache with in-memory implementation to avoid Redis
                services.AddSingleton<IDistributedCache>(sp => new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

                // Override ICatalogServiceClient to return deterministic data
                services.AddScoped<ICatalogServiceClient>(_ => new FakeCatalogClient());

                // Replace event publisher with NoOp to avoid RabbitMQ
                var existingPublisher = services.SingleOrDefault(s => s.ServiceType == typeof(IEventPublisher));
                if (existingPublisher is not null) services.Remove(existingPublisher);
                services.AddScoped<IEventPublisher, Shared.Infrastructure.Messaging.NoOpEventPublisher>();

                // Inject in-memory idempotency store for tests
                var idemDescriptor = services.SingleOrDefault(s => s.ServiceType == typeof(IIdempotencyStore));
                if (idemDescriptor is not null) services.Remove(idemDescriptor);
                services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
            });
        }
    }

    internal class FakeCatalogClient : ICatalogServiceClient
    {
        public Task<Result<bool>> DecreaseStock(List<DecreaseItemDto> items) => Task.FromResult(new Result<bool> { IsSuccess = true, Value = true });

        public Task<Result<ProductDto>> GetProductById(Guid id)
            => Task.FromResult(new Result<ProductDto> { IsSuccess = true, Value = new ProductDto { Id = id, Name = $"P-{id.ToString()[..6]}", Price = 10, Stock = 100 } });

        public Task<Result<List<ProductDto>>> GetProductsByIdList(List<Guid> ids)
            => Task.FromResult(new Result<List<ProductDto>>
            {
                IsSuccess = true,
                Value = ids.Select(id => new ProductDto { Id = id, Name = $"P-{id.ToString()[..6]}", Price = 10, Stock = 100 }).ToList()
            });

        public Task<Result<bool>> IncreaseStock(List<IncreaseItemDto> items) => Task.FromResult(new Result<bool> { IsSuccess = true, Value = true });
    }

    public class CreateOrderIntegrationTests : IClassFixture<OrderServiceWebAppFactory>
    {
        private readonly OrderServiceWebAppFactory _factory;
        public CreateOrderIntegrationTests(OrderServiceWebAppFactory factory) => _factory = factory;

        [Fact]
        public async Task CreateOrder_Returns_Success_And_Persists()
        {
            var client = _factory.CreateClient();

            var cmd = new AddOrderCommand
            {
                CustomerEmail = "john@example.com",
                Items =
                [
                    new AddOrderItemCommand { ProductId = Guid.NewGuid(), Quantity = 2 },
                    new AddOrderItemCommand { ProductId = Guid.NewGuid(), Quantity = 1 }
                ]
            };

            var resp = await client.PostAsJsonAsync("/api/v1/orders", cmd);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<Result<Guid>>();

            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBe(Guid.Empty);

            // Fetch back via API to ensure it is persisted and query works
            var detailResp = await client.GetAsync($"/api/v1/orders/{result.Value}");
            detailResp.EnsureSuccessStatusCode();
            var orderResult = await detailResp.Content.ReadFromJsonAsync<Result<Shared.Contracts.Order.OrderDto>>();
            orderResult.Should().NotBeNull();
            orderResult!.IsSuccess.Should().BeTrue();
            orderResult.Value.Should().NotBeNull();
            orderResult.Value!.Items.Count.Should().Be(2);
            orderResult.Value.TotalPrice.Should().Be(30); // 2*10 + 1*10 with FakeCatalogClient
        }

        [Fact]
        public async Task CreateOrder_With_Same_IdempotencyKey_Returns_Same_Result()
        {
            var client = _factory.CreateClient();

            var idemKey = Guid.NewGuid().ToString("N");
            var cmd = new AddOrderCommand
            {
                CustomerEmail = "john@example.com",
                IdempotencyKey = idemKey,
                Items =
                [
                    new AddOrderItemCommand { ProductId = Guid.NewGuid(), Quantity = 1 }
                ]
            };

            var resp1 = await client.PostAsJsonAsync("/api/v1/orders", cmd);
            resp1.EnsureSuccessStatusCode();
            var r1 = await resp1.Content.ReadFromJsonAsync<Result<Guid>>();
            r1!.IsSuccess.Should().BeTrue();
            r1.Value.Should().NotBe(Guid.Empty);

            // second call with same key should NOT create a new order, but return same result
            var resp2 = await client.PostAsJsonAsync("/api/v1/orders", cmd);
            resp2.EnsureSuccessStatusCode();
            var r2 = await resp2.Content.ReadFromJsonAsync<Result<Guid>>();
            r2!.IsSuccess.Should().BeTrue();
            r2.Value.Should().Be(r1.Value);
        }
    }

    internal class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<string, (DateTimeOffset expires, string value)> _store = new();
        private readonly object _lock = new();

        public Task<(bool Found, string Value)> GetAsync(string key)
        {
            lock (_lock)
            {
                if (_store.TryGetValue(key, out var entry) && entry.expires > DateTimeOffset.UtcNow)
                {
                    return Task.FromResult((true, entry.value));
                }
                return Task.FromResult((false, string.Empty));
            }
        }

        public Task RemoveAsync(string key)
        {
            lock (_lock) { _store.Remove(key); }
            return Task.CompletedTask;
        }

        public Task SetResultAsync<T>(string key, T result, TimeSpan ttl)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            lock (_lock)
            {
                _store[key] = (DateTimeOffset.UtcNow.Add(ttl), payload);
            }
            return Task.CompletedTask;
        }

        public Task<bool> TryStartProcessingAsync(string key, TimeSpan ttl)
        {
            lock (_lock)
            {
                if (_store.TryGetValue(key, out var entry) && entry.expires > DateTimeOffset.UtcNow)
                {
                    return Task.FromResult(false);
                }
                _store[key] = (DateTimeOffset.UtcNow.Add(ttl), "processing");
                return Task.FromResult(true);
            }
        }
    }
}
