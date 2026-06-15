namespace Shared.Infrastructure.Redis
{
    public interface IIdempotencyStore
    {
        Task<bool> TryStartProcessingAsync(string key, TimeSpan ttl);
        Task SetResultAsync<T>(string key, T result, TimeSpan ttl);
        Task<(bool Found, string Value)> GetAsync(string key);
        Task RemoveAsync(string key);
    }
}
