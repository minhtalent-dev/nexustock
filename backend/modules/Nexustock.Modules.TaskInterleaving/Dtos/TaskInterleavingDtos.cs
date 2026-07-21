using System;
using System.Collections.Generic;

namespace Nexustock.Modules.TaskInterleaving.Dtos;

public class NextTaskRecommendationQuery
{
    public Guid? CurrentLocationId { get; set; }
    public Guid? CurrentZoneId { get; set; }
    public string? SourceTaskType { get; set; }
    public Guid? SourceTaskId { get; set; }
    public string? OperationType { get; set; }
    public int MaxCandidates { get; set; } = 10;
}

public class TaskScoreExplanationDto
{
    public decimal DistanceScore { get; set; }
    public decimal AgeScore { get; set; }
    public decimal PriorityScore { get; set; }
    public decimal ContinuityScore { get; set; }
    public decimal PenaltyScore { get; set; }
}

public class TaskRecommendationCandidateDto
{
    public string TaskType { get; set; } = null!;
    public Guid TaskId { get; set; }
    public string OperationType { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public Guid? ZoneId { get; set; }
    public decimal Score { get; set; }
    public TaskScoreExplanationDto Explanation { get; set; } = null!;
}

public class NextTaskRecommendationResponse
{
    public Guid RecommendationId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? ExpiresAt { get; set; }
    public TaskRecommendationCandidateDto? Selected { get; set; }
    public List<TaskRecommendationCandidateDto> Candidates { get; set; } = new();
    public string? TraceId { get; set; }
}

public class AcceptTaskRecommendationRequest
{
    public string IdempotencyKey { get; set; } = null!;
    public string? AcceptedTaskVersion { get; set; }
}

public class AcceptTaskRecommendationResponse
{
    public Guid RecommendationId { get; set; }
    public string TaskType { get; set; } = null!;
    public Guid TaskId { get; set; }
    public string Status { get; set; } = null!;
    public string? AssignedToUserId { get; set; }
    public DateTime AcceptedAt { get; set; }
    public string? TraceId { get; set; }
}

public class RejectTaskRecommendationRequest
{
    public string ReasonCode { get; set; } = null!;
    public string? Note { get; set; }
}

public class RejectTaskRecommendationResponse
{
    public Guid RecommendationId { get; set; }
    public string Status { get; set; } = null!;
    public string ReasonCode { get; set; } = null!;
    public DateTime RejectedAt { get; set; }
    public string? TraceId { get; set; }
}

public class TaskRecommendationListQuery
{
    public string? Status { get; set; }
    public Guid? UserId { get; set; }
    public string? OperationType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class TaskRecommendationListItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? SourceTaskType { get; set; }
    public Guid? SourceTaskId { get; set; }
    public string Status { get; set; } = null!;
    public string? SelectedTaskType { get; set; }
    public Guid? SelectedTaskId { get; set; }
    public decimal? SelectedScore { get; set; }
    public string? ReasonCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class TaskRecommendationDetailResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? LaborSessionId { get; set; }
    public string? SourceTaskType { get; set; }
    public Guid? SourceTaskId { get; set; }
    public Guid? CurrentLocationId { get; set; }
    public Guid? CurrentZoneId { get; set; }
    public string Status { get; set; } = null!;
    public string? SelectedTaskType { get; set; }
    public Guid? SelectedTaskId { get; set; }
    public decimal? SelectedScore { get; set; }
    public string? ReasonCode { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? TraceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public List<TaskRecommendationCandidateDto> Candidates { get; set; } = new();
}

public class TaskInterleavingKpiQuery
{
    public Guid? UserId { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? ZoneId { get; set; }
    public string? OperationType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class TaskInterleavingKpiResponse
{
    public decimal AcceptRate { get; set; }
    public decimal RejectRate { get; set; }
    public decimal NoCandidateRate { get; set; }
    public decimal AverageSelectedScore { get; set; }
    public decimal AverageDecisionSeconds { get; set; }
    public decimal ConflictRate { get; set; }
    public decimal SameZoneSuggestionRate { get; set; }
}
