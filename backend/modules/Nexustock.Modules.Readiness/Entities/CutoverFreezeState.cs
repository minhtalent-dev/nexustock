using System;

namespace Nexustock.Modules.Readiness.Entities;

public class CutoverFreezeState
{
    public Guid TenantId { get; set; }
    public bool IsFrozen { get; set; }
    public DateTimeOffset? FrozenAt { get; set; }
    public string? FrozenBy { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
