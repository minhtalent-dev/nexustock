using System;

namespace Nexustock.Modules.CrossDocking.Entities;

public enum CrossDockCandidateStatus
{
    Pending,
    Accepted,
    Rejected,
    Expired,
    Executing
}

public class CrossDockCandidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LotId { get; set; }
    public Guid InboundOrderItemId { get; set; }
    public Guid WaveItemId { get; set; }
    public Guid ItemId { get; set; }
    public decimal QtyAvailable { get; set; }
    public decimal QtyRequested { get; set; }
    public decimal QtyMatched { get; set; }
    public int MatchScore { get; set; }
    public CrossDockCandidateStatus Status { get; set; } = CrossDockCandidateStatus.Pending;
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? RejectedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
