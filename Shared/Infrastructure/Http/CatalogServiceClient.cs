using Shared.Application.Result;
using Shared.Contracts.Catalog.Dtos;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace Shared.Infrastructure.Http
{
    public class CatalogServiceClient : ICatalogServiceClient
    {
        private readonly HttpClient _http;
        private readonly IDistributedCache? _cache;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private class UpstreamResult<T>
        {
            public bool IsSuccess { get; set; }
            public string? ErrorMessage { get; set; }
            public T? Value { get; set; }
        }

        public CatalogServiceClient(HttpClient http, IDistributedCache? cache = null)
        {
            _http = http;
            _cache = cache;
        }

        public async Task<Result<ProductDto>> GetProductById(Guid id)
        {
            var cacheKey = $"catalog:product:{id}";
            if (_cache is not null)
            {
                var cached = await _cache.GetAsync(cacheKey);
                if (cached is not null)
                {
                    var cachedDto = JsonSerializer.Deserialize<ProductDto>(Encoding.UTF8.GetString(cached), _jsonOptions);
                    if (cachedDto is not null)
                        return new Result<ProductDto> { IsSuccess = true, ErrorMessage = string.Empty, Value = cachedDto };
                }
            }

            var resp = await _http.GetAsync($"api/v1/products/{id}");
            if (!resp.IsSuccessStatusCode)
                return new Result<ProductDto> { IsSuccess = false, ErrorMessage = $"Upstream error {resp.StatusCode}", Value = null };

            var content = await resp.Content.ReadAsStringAsync();
            // Try Result envelope first
            ProductDto? dto = null;
            try
            {
                var wrapped = JsonSerializer.Deserialize<UpstreamResult<ProductDto>>(content, _jsonOptions);
                if (wrapped is not null)
                {
                    if (wrapped.IsSuccess && wrapped.Value is not null)
                    {
                        dto = wrapped.Value;
                    }
                    else if (!wrapped.IsSuccess)
                    {
                        return new Result<ProductDto> { IsSuccess = false, ErrorMessage = wrapped.ErrorMessage ?? "Upstream failure", Value = null };
                    }
                }
            }
            catch { }
            dto ??= JsonSerializer.Deserialize<ProductDto>(content, _jsonOptions);

            if (dto is not null && _cache is not null)
            {
                var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto, _jsonOptions));
                await _cache.SetAsync(cacheKey, payload, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
            }

            return new Result<ProductDto> { IsSuccess = dto is not null, ErrorMessage = dto is null ? "Deserialization failed" : string.Empty, Value = dto };
        }

        public async Task<Result<List<ProductDto>>> GetProductsByIdList(List<Guid> ids)
        {
            if (ids == null || ids.Count == 0)
                return new Result<List<ProductDto>> { IsSuccess = true, ErrorMessage = string.Empty, Value = new List<ProductDto>() };

            // Simple approach: POST the ids to an endpoint
            var payload = JsonSerializer.Serialize(ids, _jsonOptions);
            var resp = await _http.PostAsync("api/v1/products/batch", new StringContent(payload, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
                return new Result<List<ProductDto>> { IsSuccess = false, ErrorMessage = $"Upstream error {resp.StatusCode}", Value = null };

            var content = await resp.Content.ReadAsStringAsync();
            // Try unwrap Result envelope
            try
            {
                var wrapped = JsonSerializer.Deserialize<UpstreamResult<List<ProductDto>>>(content, _jsonOptions);
                if (wrapped is not null)
                {
                    if (wrapped.IsSuccess && wrapped.Value is not null)
                        return new Result<List<ProductDto>> { IsSuccess = true, ErrorMessage = string.Empty, Value = wrapped.Value };
                    return new Result<List<ProductDto>> { IsSuccess = false, ErrorMessage = wrapped?.ErrorMessage ?? "Upstream failure", Value = null };
                }
            }
            catch { }

            var dtos = JsonSerializer.Deserialize<List<ProductDto>>(content, _jsonOptions);
            return new Result<List<ProductDto>> { IsSuccess = dtos is not null, ErrorMessage = dtos is null ? "Deserialization failed" : string.Empty, Value = dtos };
        }

        public async Task<Result<bool>> DecreaseStock(List<DecreaseItemDto> items)
        {
            if (items == null || items.Count == 0) return new Result<bool> { IsSuccess = false, ErrorMessage = "Invalid arguments", Value = false };

            var payload = JsonSerializer.Serialize(new { Items = items }, _jsonOptions);
            var resp = await _http.PostAsync($"api/v1/products/decrease", new StringContent(payload, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
                return new Result<bool> { IsSuccess = false, ErrorMessage = $"Upstream error {resp.StatusCode}", Value = false };

            var content = await resp.Content.ReadAsStringAsync();
            try
            {
                var wrapped = JsonSerializer.Deserialize<UpstreamResult<bool>>(content, _jsonOptions);
                if (wrapped is not null)
                {
                    if (!wrapped.IsSuccess || wrapped.Value == false)
                        return new Result<bool> { IsSuccess = false, ErrorMessage = wrapped.ErrorMessage ?? "Stock decrease failed", Value = false };
                }
            }
            catch { }

            // Invalidate cache for these products on success
            if (_cache is not null)
            {
                foreach (var item in items.Select(i => i.ProductId).Distinct())
                {
                    var cacheKey = $"catalog:product:{item}";
                    await _cache.RemoveAsync(cacheKey);
                }
            }

            return new Result<bool> { IsSuccess = true, ErrorMessage = string.Empty, Value = true };
        }

        public async Task<Result<bool>> IncreaseStock(List<IncreaseItemDto> items)
        {
            if (items == null || items.Count == 0) return new Result<bool> { IsSuccess = false, ErrorMessage = "Invalid arguments", Value = false };

            var payload = JsonSerializer.Serialize(new { Items = items }, _jsonOptions);
            var resp = await _http.PostAsync($"api/v1/products/increase", new StringContent(payload, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
                return new Result<bool> { IsSuccess = false, ErrorMessage = $"Upstream error {resp.StatusCode}", Value = false };

            var content = await resp.Content.ReadAsStringAsync();
            try
            {
                var wrapped = JsonSerializer.Deserialize<UpstreamResult<bool>>(content, _jsonOptions);
                if (wrapped is not null)
                {
                    if (!wrapped.IsSuccess || wrapped.Value == false)
                        return new Result<bool> { IsSuccess = false, ErrorMessage = wrapped.ErrorMessage ?? "Stock increase failed", Value = false };
                }
            }
            catch { }

            // Invalidate cache for these products on success
            if (_cache is not null)
            {
                foreach (var item in items.Select(i => i.ProductId).Distinct())
                {
                    var cacheKey = $"catalog:product:{item}";
                    await _cache.RemoveAsync(cacheKey);
                }
            }

            return new Result<bool> { IsSuccess = true, ErrorMessage = string.Empty, Value = true };
        }
    }
}
