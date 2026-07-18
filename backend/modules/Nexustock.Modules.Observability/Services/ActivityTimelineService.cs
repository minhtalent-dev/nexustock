using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Entities;

namespace Nexustock.Modules.Observability.Services;

public class ActivityTimelineService : IActivityTimelineService
{
    private readonly ObservabilityDbContext _db;
    private readonly ILogger<ActivityTimelineService> _logger;

    public ActivityTimelineService(ObservabilityDbContext db, ILogger<ActivityTimelineService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        string eventType,
        string title,
        string? description,
        string severity,
        string traceId,
        object? metadata,
        CancellationToken ct = default)
    {
        try
        {
            var cleanDesc = SensitiveDataMasker.Mask(description);
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

            var entry = new ActivityTimelineEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntityType = entityType,
                EntityId = entityId,
                EventType = eventType,
                Title = title,
                Description = cleanDesc,
                Severity = severity,
                TraceId = traceId,
                MetadataJson = string.IsNullOrEmpty(metadataStr) ? null : metadataStr,
                CreatedAt = DateTime.UtcNow
            };

            _db.ActivityTimelineEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Không throw để tránh ảnh hưởng đến transaction chính
            _logger.LogWarning(ex, "[ActivityTimeline] Lỗi ghi nhận timeline event={Event} entity={EntityId}. Bỏ qua.", eventType, entityId);
        }
    }
}
