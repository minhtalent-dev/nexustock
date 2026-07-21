using System;

namespace Nexustock.Modules.TaskInterleaving.Entities;

public class TaskRecommendationCandidate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecommendationId { get; set; }
    public string TaskType { get; set; } = null!;
    public Guid TaskId { get; set; }
    public string OperationType { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public Guid? ZoneId { get; set; }
    public string TaskStatus { get; set; } = null!;
    public int Priority { get; set; }
    public int AgeSeconds { get; set; }
    public decimal DistanceScore { get; set; }
    public decimal AgeScore { get; set; }
    public decimal PriorityScore { get; set; }
    public decimal ContinuityScore { get; set; }
    public decimal PenaltyScore { get; set; }
    public decimal TotalScore { get; set; }
    public string Explanation { get; set; } = null!; // JSON format string for detailed breakdown
    public DateTime CreatedAt { get; set; }
}
