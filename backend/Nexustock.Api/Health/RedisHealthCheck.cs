using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nexustock.Api.Health;

public sealed class RedisHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var parts = connectionString.Split(':', 2, StringSplitOptions.TrimEntries);
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var parsedPort) ? parsedPort : 6379;

            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            await using var stream = client.GetStream();
            var ping = Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n");
            await stream.WriteAsync(ping, cancellationToken);

            var buffer = new byte[16];
            var read = await stream.ReadAsync(buffer, cancellationToken);
            var response = Encoding.ASCII.GetString(buffer, 0, read);

            return response.StartsWith("+PONG", StringComparison.OrdinalIgnoreCase)
                ? HealthCheckResult.Healthy("Redis connection is healthy.")
                : HealthCheckResult.Unhealthy("Redis PING returned unexpected response.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis connection failed.", ex);
        }
    }
}
