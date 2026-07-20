using System;

namespace Nexustock.Modules.CrossDocking.Entities;

public enum CrossDockEventType
{
    Evaluated,
    Accepted,
    Rejected,
    Expired,
    Executed
}

public class CrossDockEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CandidateId { get; set; }
    public CrossDockEventType EventType { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public CrossDockCandidate Candidate { get; set; } = null!;
}
