using StackExchange.Redis;
using System.Text.Json;

namespace Shared.Redis
{
    public class IdempotencyStore
    {
        private readonly IDatabase _db;

        public IdempotencyStore(IConnectionMultiplexer multiplexer)
        {
            _db = multiplexer.GetDatabase();
        }

        // Try to create a processing placeholder. Returns true if created, false if key existed.
        public async Task<bool> TryStartProcessingAsync(string key, TimeSpan ttl)
        {
            // use set if not exists
            return await _db.StringSetAsync(GetKey(key), "processing", ttl, When.NotExists);
        }

        public async Task SetResultAsync<T>(string key, T result, TimeSpan ttl)
        {
            var payload = JsonSerializer.Serialize(result);
            await _db.StringSetAsync(GetKey(key), payload, ttl);
        }

        public async Task<(bool Found, string Value)> GetAsync(string key)
        {
            var val = await _db.StringGetAsync(GetKey(key));
            if (val.IsNullOrEmpty) return (false, string.Empty);
            return (true, val.ToString());
        }

        public async Task RemoveAsync(string key)
        {
            await _db.KeyDeleteAsync(GetKey(key));
        }

        private static string GetKey(string key) => $"idem:{key}";
    }
}
