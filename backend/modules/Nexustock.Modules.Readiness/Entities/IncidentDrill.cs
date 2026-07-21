using System;

namespace Nexustock.Modules.Readiness.Entities;

public class IncidentDrill
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ScenarioCode { get; set; } = null!;
    public int RtoMinutes { get; set; }
    public bool Passed { get; set; }
    public string ConductedBy { get; set; } = null!;
    public DateTimeOffset ConductedAt { get; set; }
    public string? EvidenceNote { get; set; }
    public string? TraceId { get; set; }
}
