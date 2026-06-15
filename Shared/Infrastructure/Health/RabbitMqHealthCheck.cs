using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.Infrastructure.Health
{
    public class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;

        public RabbitMqHealthCheck(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var host = _configuration["RabbitMq:Host"] ?? _configuration["RabbitMQ:Host"];
                if (string.IsNullOrWhiteSpace(host))
                {
                    // If not configured, treat as Healthy (dependency not required for this service)
                    return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ not configured"));
                }

                var portStr = _configuration["RabbitMq:Port"] ?? _configuration["RabbitMQ:Port"];
                var port = 5672;
                if (int.TryParse(portStr, out var parsed)) port = parsed;

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                var completed = Task.WhenAny(connectTask, timeoutTask).GetAwaiter().GetResult();

                if (completed == connectTask && client.Connected)
                {
                    return Task.FromResult(HealthCheckResult.Healthy());
                }
                return Task.FromResult(HealthCheckResult.Unhealthy($"RabbitMQ TCP connect to {host}:{port} timed out or failed"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ TCP connection failure", ex));
            }
        }
    }
}
