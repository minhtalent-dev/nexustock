using System;

namespace Nexustock.Modules.Observability.Entities;

/// <summary>
/// Thực thể ghi nhận cảnh báo vận hành hệ thống.
/// </summary>
public class OperationalAlert
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string AlertType { get; set; } = null!; // webhook.dlqThreshold, kpi.stale, exception.aging
    public string Severity { get; set; } = "warning"; // warning, critical
    public string Status { get; set; } = "open"; // open, acknowledged, resolved
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string SourceModule { get; set; } = null!;
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public string? TraceId { get; set; }
    public decimal? MetricValue { get; set; }
    public decimal? ThresholdValue { get; set; }
    public Guid? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
