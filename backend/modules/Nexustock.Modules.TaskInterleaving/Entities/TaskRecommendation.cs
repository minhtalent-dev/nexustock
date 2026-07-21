using System;

namespace Nexustock.Modules.TaskInterleaving.Entities;

public class TaskRecommendation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? LaborSessionId { get; set; }
    public string? SourceTaskType { get; set; }
    public Guid? SourceTaskId { get; set; }
    public Guid? CurrentLocationId { get; set; }
    public Guid? CurrentZoneId { get; set; }
    public string Status { get; set; } = "Open"; // Open, Accepted, Rejected, Expired, Superseded, NoCandidate
    public string? SelectedTaskType { get; set; }
    public Guid? SelectedTaskId { get; set; }
    public decimal? SelectedScore { get; set; }
    public string? ReasonCode { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? TraceId { get; set; }
    public string? AcceptIdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
