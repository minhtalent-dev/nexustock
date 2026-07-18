using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nexustock.Modules.Observability.Services;

public interface IActivityTimelineService
{
    Task RecordAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        string eventType,
        string title,
        string? description,
        string severity,
        string traceId,
        object? metadata,
        CancellationToken ct = default);
}
