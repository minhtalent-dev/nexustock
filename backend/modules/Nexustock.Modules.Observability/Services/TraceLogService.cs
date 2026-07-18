using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Entities;

namespace Nexustock.Modules.Observability.Services;

public class TraceLogService : ITraceLogService
{
    private readonly ObservabilityDbContext _db;
    private readonly ITraceContext _traceContext;
    private readonly ILogger<TraceLogService> _logger;

    public TraceLogService(
        ObservabilityDbContext db, 
        ITraceContext traceContext,
        ILogger<TraceLogService> logger)
    {
        _db = db;
        _traceContext = traceContext;
        _logger = logger;
    }

    public async Task WriteLogAsync(
        Guid? tenantId,
        string spanName,
        string source,
        string level,
        string message,
        int? durationMs = null,
        object? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            var traceId = _traceContext.GetCurrentTraceId();
            var cleanMsg = SensitiveDataMasker.Mask(message);
            var metadataStr = string.Empty;

            if (metadata != null)
            {
                if (metadata is string rawStr)
                {
                    metadataStr = SensitiveDataMasker.Mask(rawStr);
                }
                else
                {
                    var rawJson = JsonSerializer.Serialize(metadata);
                    metadataStr = SensitiveDataMasker.Mask(rawJson);
                }
            }

            var logEntry = new TraceLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TraceId = traceId,
                SpanName = spanName,
                Source = source,
                Level = level,
                Message = cleanMsg,
                DurationMs = durationMs,
                MetadataJson = string.IsNullOrEmpty(metadataStr) ? null : metadataStr,
                CreatedAt = DateTime.UtcNow
            };

            _db.TraceLogs.Add(logEntry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TraceLog] Không thể ghi trace log. Bỏ qua.");
        }
    }
}
