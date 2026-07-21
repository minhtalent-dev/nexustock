using System;

namespace Nexustock.Modules.Readiness.Entities;

public class CutoverLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string StepCode { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string Actor { get; set; } = null!;
    public string? Note { get; set; }
    public string? TraceId { get; set; }
}
