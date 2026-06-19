using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Net;

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
            if (!res.IsSuccessStatusCode)
            {
                var msg = await ExtractErrorMessage(res);
                throw new HttpRequestException(msg);
            }
            return await ReadAsJsonAsync<T>(res);
        }

        public async Task<T?> PostAsync<T>(string clientName, string url, object body)
        {
            var client = _factory.CreateClient(clientName);
            AttachJwt(client);
            var res = await client.PostAsJsonAsync(url, body);
            if (!res.IsSuccessStatusCode)
            {
                var msg = await ExtractErrorMessage(res);
                throw new HttpRequestException(msg);
            }
            return await ReadAsJsonAsync<T>(res);
        }

        public async Task<T?> PutAsync<T>(string clientName, string url, object body)
        {
            var client = _factory.CreateClient(clientName);
            AttachJwt(client);
            var res = await client.PutAsJsonAsync(url, body);
            if (!res.IsSuccessStatusCode)
            {
                var msg = await ExtractErrorMessage(res);
                throw new HttpRequestException(msg);
            }
            return await ReadAsJsonAsync<T>(res);
        }

        public async Task<T?> DeleteAsync<T>(string clientName, string url)
        {
            var client = _factory.CreateClient(clientName);
            AttachJwt(client);
            var res = await client.DeleteAsync(url);
            if (!res.IsSuccessStatusCode)
            {
                var msg = await ExtractErrorMessage(res);
                throw new HttpRequestException(msg);
            }
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
                // Respect Result<T> error pattern
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("isSuccess", out var okEl))
                {
                    if (okEl.ValueKind == JsonValueKind.False)
                    {
                        var msg = root.TryGetProperty("errorMessage", out var errEl) && errEl.ValueKind == JsonValueKind.String
                            ? errEl.GetString()
                            : "Request failed";
                        throw new HttpRequestException(msg ?? "Request failed");
                    }
                }
                // Unwrap Result envelope { value: ... }
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var valueEl))
                {
                    var valueJson = valueEl.GetRawText();
                    return JsonSerializer.Deserialize<T>(valueJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                // Unwrap pagination envelope { data: [...], meta: { ... } }
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataEl))
                {
                    var dataJson = dataEl.GetRawText();
                    return JsonSerializer.Deserialize<T>(dataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch
            {
                // fall back to direct deserialize
            }

            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private static async Task<string> ExtractErrorMessage(HttpResponseMessage res)
        {
            string fallback = $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}";
            string content = string.Empty;
            try
            {
                content = await res.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(content)) return fallback;

                // Try JSON first
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        // Result pattern
                        if (root.TryGetProperty("isSuccess", out var okEl) && okEl.ValueKind == JsonValueKind.False)
                        {
                            if (root.TryGetProperty("errorMessage", out var errEl) && errEl.ValueKind == JsonValueKind.String)
                                return errEl.GetString() ?? fallback;
                        }
                        // ProblemDetails
                        if (root.TryGetProperty("detail", out var detailEl) && detailEl.ValueKind == JsonValueKind.String)
                            return detailEl.GetString() ?? fallback;
                        if (root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
                            return titleEl.GetString() ?? fallback;
                        // Generic message fields
                        if (root.TryGetProperty("errorMessage", out var emEl) && emEl.ValueKind == JsonValueKind.String)
                            return emEl.GetString() ?? fallback;
                        if (root.TryGetProperty("message", out var mEl) && mEl.ValueKind == JsonValueKind.String)
                            return mEl.GetString() ?? fallback;
                        // ModelState errors: { errors: { field: ["msg1", ...] } }
                        if (root.TryGetProperty("errors", out var errorsEl) && errorsEl.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in errorsEl.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in prop.Value.EnumerateArray())
                                    {
                                        if (item.ValueKind == JsonValueKind.String)
                                            return item.GetString() ?? fallback;
                                    }
                                }
                            }
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.String)
                    {
                        return root.GetString() ?? fallback;
                    }
                }
                catch
                {
                    // not json, fall through
                }

                // Plain text body
                var text = content.Trim('"', '\'', '\n', '\r', '\t', ' ');
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            catch
            {
                // ignore and use fallback
            }

            return fallback;
        }
    }
}
