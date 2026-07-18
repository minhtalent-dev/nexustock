using System;

namespace Nexustock.Modules.Observability.Entities;

/// <summary>
/// Thực thể ghi nhận log truy vết (Trace) xuyên suốt.
/// </summary>
public class TraceLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string TraceId { get; set; } = null!;
    public string SpanName { get; set; } = null!;
    public string Source { get; set; } = null!; // api, job, webhook, frontend
    public string Level { get; set; } = "info"; // info, warning, error
    public string Message { get; set; } = null!;
    public int? DurationMs { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
