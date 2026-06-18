using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http.Headers;
using System.Net;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace Shared.Infrastructure.Http
{
    public static class CatalogServiceClientExtensions
    {
        public static IServiceCollection AddCatalogServiceClient(this IServiceCollection services, string baseAddress)
        {
            services.TryAddTransient<CorrelationIdDelegatingHandler>();
            services.TryAddTransient<AuthorizationDelegatingHandler>();
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>(client =>
            {
                client.BaseAddress = new Uri(baseAddress);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            // Ensure authorization header is forwarded from incoming request when present
            .AddHttpMessageHandler<AuthorizationDelegatingHandler>()
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => (int)msg.StatusCode == 429)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

    // DelegatingHandler that ensures X-Correlation-Id header is propagated to downstream services
    internal sealed class CorrelationIdDelegatingHandler : DelegatingHandler
    {
        private const string HeaderName = "X-Correlation-Id";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var ctx = _httpContextAccessor.HttpContext;
            string correlationId = string.Empty;
            if (ctx != null && ctx.Request.Headers.TryGetValue(HeaderName, out StringValues values) && !StringValues.IsNullOrEmpty(values))
            {
                correlationId = values.ToString();
            }
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                // generate one to ensure continuity even when the inbound request didn't have it
                correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
            }
            if (!request.Headers.Contains(HeaderName))
            {
                request.Headers.Add(HeaderName, correlationId);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }

    // DelegatingHandler that forwards the Authorization header (Bearer token) from the incoming HTTP context
    internal sealed class AuthorizationDelegatingHandler : DelegatingHandler
    {
        private const string AuthorizationHeader = "Authorization";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthorizationDelegatingHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx != null && ctx.Request.Headers.TryGetValue(AuthorizationHeader, out var values) && !StringValues.IsNullOrEmpty(values))
            {
                var val = values.ToString();
                if (!request.Headers.Contains(AuthorizationHeader))
                {
                    request.Headers.Add(AuthorizationHeader, val);
                }
            }
            return base.SendAsync(request, cancellationToken);
        }
    }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
        }
    }
}
