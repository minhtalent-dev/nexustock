using System;

namespace Nexustock.Modules.Readiness.Entities;

public class UatRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ScenarioCode { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public string? ResultNote { get; set; }
    public string? SignedOffBy { get; set; }
    public DateTimeOffset? SignedOffAt { get; set; }
    public string? EvidenceUrl { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
