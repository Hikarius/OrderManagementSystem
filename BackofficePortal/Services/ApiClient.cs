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
            var stream = await res.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<T?> PostAsync<T>(string clientName, string url, object body)
        {
            var client = _factory.CreateClient(clientName);
            AttachJwt(client);
            var res = await client.PostAsJsonAsync(url, body);
            res.EnsureSuccessStatusCode();
            var stream = await res.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
