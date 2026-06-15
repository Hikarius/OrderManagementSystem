using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace BackofficePortal.Services
{
    public class ApiClient
    {
        private readonly IHttpClientFactory _factory;
        private readonly IHttpContextAccessor _accessor;

        public ApiClient(IHttpClientFactory factory, IHttpContextAccessor accessor)
        {
            _factory = factory;
            _accessor = accessor;
        }

        private void AttachJwt(HttpClient client)
        {
            var token = _accessor.HttpContext?.Session.GetString("jwt");
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<T?> GetAsync<T>(string clientName, string url)
        {
            var client = _factory.CreateClient(clientName);
            AttachJwt(client);
            var res = await client.GetAsync(url);
            res.EnsureSuccessStatusCode();
            return await ReadAsJsonAsync<T>(res);
        }

        public async Task<T?> PostAsync<T>(string clientName, string url, object body)
        {
            var client = _factory.CreateClient(clientName);
            AttachJwt(client);
            var res = await client.PostAsJsonAsync(url, body);
            res.EnsureSuccessStatusCode();
            return await ReadAsJsonAsync<T>(res);
        }

        public async Task<T?> PutAsync<T>(string clientName, string url, object body)
        {
            var client = _factory.CreateClient(clientName);
            AttachJwt(client);
            var res = await client.PutAsJsonAsync(url, body);
            res.EnsureSuccessStatusCode();
            return await ReadAsJsonAsync<T>(res);
        }

        public async Task<T?> DeleteAsync<T>(string clientName, string url)
        {
            var client = _factory.CreateClient(clientName);
            AttachJwt(client);
            var res = await client.DeleteAsync(url);
            res.EnsureSuccessStatusCode();
            return await ReadAsJsonAsync<T>(res);
        }

        private static async Task<T?> ReadAsJsonAsync<T>(HttpResponseMessage res)
        {
            var json = await res.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return default;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var valueEl))
                {
                    var valueJson = valueEl.GetRawText();
                    return JsonSerializer.Deserialize<T>(valueJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch
            {
                // fall back to direct deserialize
            }

            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
