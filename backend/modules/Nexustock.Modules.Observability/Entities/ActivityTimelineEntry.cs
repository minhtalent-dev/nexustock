using System;

namespace Nexustock.Modules.Observability.Entities;

/// <summary>
/// Thực thể ghi nhận lịch sử/timeline hoạt động nghiệp vụ.
/// </summary>
public class ActivityTimelineEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string EventType { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Severity { get; set; } = "info"; // info, warning, critical
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string TraceId { get; set; } = null!;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
