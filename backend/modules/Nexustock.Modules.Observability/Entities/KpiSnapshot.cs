using System;

namespace Nexustock.Modules.Observability.Entities;

/// <summary>
/// Thực thể ghi nhận các chỉ số KPI được chụp lại định kỳ.
/// </summary>
public class KpiSnapshot
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string MetricKey { get; set; } = null!;
    public string MetricGroup { get; set; } = null!; // warehouse, integration, exception, inventory
    public decimal Value { get; set; }
    public string Unit { get; set; } = null!; // count, percent, minutes
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string SourceModule { get; set; } = null!;
    public DateTime ComputedAt { get; set; }
    public string? MetadataJson { get; set; }
}
