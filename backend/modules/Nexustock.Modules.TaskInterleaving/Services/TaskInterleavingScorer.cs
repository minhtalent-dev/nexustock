using System;
using System.Collections.Generic;

namespace Nexustock.Modules.TaskInterleaving.Services;

/// <summary>
/// Scoring heuristic v1 — deterministic, explainable, pure (không I/O).
/// </summary>
public static class TaskInterleavingScorer
{
    public const decimal MaxDistance = 45m;
    public const decimal MaxAge = 20m;
    public const decimal MaxPriority = 20m;
    public const decimal MaxContinuity = 15m;
    public const decimal MaxPenalty = 50m;
    public const int StaleAgeSeconds = 4 * 60 * 60; // 4 hours

    public sealed class ScoreInput
    {
        public Guid? CurrentLocationId { get; init; }
        public Guid? CurrentZoneId { get; init; }
        public Guid? CandidateLocationId { get; init; }
        public Guid? CandidateZoneId { get; init; }
        public int AgeSeconds { get; init; }
        public string? Step { get; init; }
        public bool HasActiveSession { get; init; }
        public bool SameOperation { get; init; }
        public bool SameZoneAsContext { get; init; }
        public bool IsConflictRisk { get; init; }
    }

    public sealed class ScoreResult
    {
        public decimal DistanceScore { get; init; }
        public decimal AgeScore { get; init; }
        public decimal PriorityScore { get; init; }
        public decimal ContinuityScore { get; init; }
        public decimal PenaltyScore { get; init; }
        public decimal TotalScore { get; init; }
    }

    public static ScoreResult Score(ScoreInput input)
    {
        var distanceScore = ComputeDistanceScore(input);
        var ageScore = ComputeAgeScore(input.AgeSeconds);
        var priorityScore = ComputePriorityScore(input.Step);
        var continuityScore = ComputeContinuityScore(input);
        var penaltyScore = ComputePenaltyScore(input);

        return new ScoreResult
        {
            DistanceScore = distanceScore,
            AgeScore = ageScore,
            PriorityScore = priorityScore,
            ContinuityScore = continuityScore,
            PenaltyScore = penaltyScore,
            TotalScore = distanceScore + ageScore + priorityScore + continuityScore - penaltyScore
        };
    }

    public static decimal ComputeDistanceScore(ScoreInput input)
    {
        if (!input.CurrentLocationId.HasValue || !input.CandidateLocationId.HasValue
            || !input.CurrentZoneId.HasValue || !input.CandidateZoneId.HasValue)
        {
            // Thiếu tọa độ / location context
            if (!input.CandidateLocationId.HasValue && !input.CurrentLocationId.HasValue)
                return 20m;
            if (!input.CandidateLocationId.HasValue || !input.CurrentLocationId.HasValue)
                return 20m;
        }

        if (input.CurrentLocationId.HasValue && input.CandidateLocationId.HasValue
            && input.CurrentLocationId.Value == input.CandidateLocationId.Value)
        {
            return 45m;
        }

        if (input.CurrentZoneId.HasValue && input.CandidateZoneId.HasValue
            && input.CurrentZoneId.Value == input.CandidateZoneId.Value)
        {
            return 35m;
        }

        if (input.CurrentZoneId.HasValue && input.CandidateZoneId.HasValue
            && input.CurrentZoneId.Value != input.CandidateZoneId.Value)
        {
            return 10m;
        }

        return 20m;
    }

    public static decimal ComputeAgeScore(int ageSeconds)
    {
        var ageMinutes = ageSeconds / 60m;
        return Math.Min(MaxAge, ageMinutes / 3m);
    }

    public static decimal ComputePriorityScore(string? step)
    {
        return step?.ToUpperInvariant() switch
        {
            "HIGH" => 20m,
            "MEDIUM" => 10m,
            "LOW" => 5m,
            _ => 0m
        };
    }

    public static decimal ComputeContinuityScore(ScoreInput input)
    {
        decimal score = 0;
        if (input.SameOperation) score += 8m;
        if (input.HasActiveSession) score += 4m;
        if (input.SameZoneAsContext) score += 3m;
        return Math.Min(MaxContinuity, score);
    }

    public static decimal ComputePenaltyScore(ScoreInput input)
    {
        decimal penalty = 0;
        if (input.AgeSeconds > StaleAgeSeconds) penalty += 20m;
        if (!input.CandidateLocationId.HasValue) penalty += 5m;
        if (input.IsConflictRisk) penalty += 50m;
        return Math.Min(MaxPenalty, penalty);
    }

    public static int CompareTieBreak(
        decimal totalScoreA, decimal priorityA, int ageSecondsA, Guid taskIdA,
        decimal totalScoreB, decimal priorityB, int ageSecondsB, Guid taskIdB)
    {
        var cmp = totalScoreB.CompareTo(totalScoreA); // DESC
        if (cmp != 0) return cmp;
        cmp = priorityB.CompareTo(priorityA); // DESC
        if (cmp != 0) return cmp;
        cmp = ageSecondsB.CompareTo(ageSecondsA); // DESC
        if (cmp != 0) return cmp;
        return taskIdA.CompareTo(taskIdB); // ASC
    }

    public static readonly HashSet<string> AllowedRejectReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "TOO_FAR", "BLOCKED_LOCATION", "EQUIPMENT_UNAVAILABLE", "TASK_CONTEXT_SWITCH",
        "SUPERVISOR_OVERRIDE", "NO_ELIGIBLE_TASK", "TASK_EXPIRED", "TASK_CONFLICT"
    };

    public static bool IsValidRejectReason(string? reasonCode)
        => !string.IsNullOrWhiteSpace(reasonCode) && AllowedRejectReasons.Contains(reasonCode);
}
