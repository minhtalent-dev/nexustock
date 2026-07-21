using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Nexustock.Modules.Readiness.Dtos;

namespace Nexustock.Modules.Readiness.Services;

public sealed class ReadinessProbeService : IReadinessProbeService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReadinessProbeService> _logger;

    public ReadinessProbeService(IConfiguration configuration, ILogger<ReadinessProbeService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ReadinessProbeResponse> ProbeAsync(string? traceId, CancellationToken ct = default)
    {
        var components = new List<ProbeComponentDto>
        {
            await ProbeDatabaseAsync(ct),
            await ProbeRedisAsync(ct),
            ProbeSap(),
            await ProbeLocalAgentAsync(ct)
        };

        var db = components[0].Status;
        var redis = components[1].Status;
        var overall = "Ready";
        if (db == "Down" || redis == "Down")
            overall = "NotReady";
        else if (components.Skip(2).Any(c => c.Status is "Skipped" or "Down" or "Degraded"))
            overall = "Degraded";

        _logger.LogInformation(
            "Event={Event} Overall={Overall} TraceId={TraceId}",
            "readiness.probe.completed", overall, traceId);

        return new ReadinessProbeResponse(overall, components, traceId);
    }

    private async Task<ProbeComponentDto> ProbeDatabaseAsync(CancellationToken ct)
    {
        var cs = _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return new ProbeComponentDto("Database", "Down", "Connection string missing");

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("select 1", conn);
            await cmd.ExecuteScalarAsync(ct);
            return new ProbeComponentDto("Database", "Up", null);
        }
        catch (Exception ex)
        {
            return new ProbeComponentDto("Database", "Down", ex.Message);
        }
    }

    private async Task<ProbeComponentDto> ProbeRedisAsync(CancellationToken ct)
    {
        var enableRedis = _configuration.GetValue("ENABLE_REDIS", false);
        if (!enableRedis)
            return new ProbeComponentDto("Redis", "Skipped", "ENABLE_REDIS=false");

        var redisConnection = _configuration.GetValue<string>("REDIS_CONNECTION") ?? "localhost:6379";
        try
        {
            var parts = redisConnection.Split(':', 2, StringSplitOptions.TrimEntries);
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 6379;

            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(host, port, cts.Token);
            await using var stream = client.GetStream();
            var ping = Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n");
            await stream.WriteAsync(ping, cts.Token);
            var buffer = new byte[16];
            var read = await stream.ReadAsync(buffer, cts.Token);
            var response = Encoding.ASCII.GetString(buffer, 0, read);
            return response.StartsWith("+PONG", StringComparison.OrdinalIgnoreCase)
                ? new ProbeComponentDto("Redis", "Up", null)
                : new ProbeComponentDto("Redis", "Down", "Unexpected PING response");
        }
        catch (Exception ex)
        {
            return new ProbeComponentDto("Redis", "Down", ex.Message);
        }
    }

    private static ProbeComponentDto ProbeSap()
    {
        // AC-08 waived / sandbox chưa sẵn — không fail gate nội bộ
        return new ProbeComponentDto("SAP", "Skipped", "AC08_WAIVED_OR_UNAVAILABLE");
    }

    private async Task<ProbeComponentDto> ProbeLocalAgentAsync(CancellationToken ct)
    {
        var url = _configuration.GetValue<string>("LOCAL_AGENT_PROBE_URL");
        if (string.IsNullOrWhiteSpace(url))
            return new ProbeComponentDto("LocalAgent", "Skipped", "LOCAL_AGENT_PROBE_URL not configured");

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var resp = await http.GetAsync(url, cts.Token);
            return resp.IsSuccessStatusCode
                ? new ProbeComponentDto("LocalAgent", "Up", null)
                : new ProbeComponentDto("LocalAgent", "Degraded", $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return new ProbeComponentDto("LocalAgent", "Degraded", ex.Message);
        }
    }
}
