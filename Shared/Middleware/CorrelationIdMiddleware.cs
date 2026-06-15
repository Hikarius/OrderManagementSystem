using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Shared.Middleware
{
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-Id";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = context.Request.Headers[HeaderName].ToString();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
                context.Request.Headers[HeaderName] = correlationId;
            }

            // Ensure the response also carries the correlation id
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(HeaderName))
                    context.Response.Headers.Add(HeaderName, correlationId);
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
