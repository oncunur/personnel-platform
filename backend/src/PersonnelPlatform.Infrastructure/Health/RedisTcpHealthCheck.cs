using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PersonnelPlatform.Infrastructure.Health;

public sealed class RedisTcpHealthCheck(IOptions<RedisOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(settings.Host, settings.Port, cancellationToken);
            return HealthCheckResult.Healthy($"Redis TCP endpoint {settings.Host}:{settings.Port} is reachable.");
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Redis TCP endpoint is not reachable.", exception);
        }
    }
}
