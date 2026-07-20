using System;
using System.Collections.Generic;

namespace Nexustock.Modules.LaborTracking.DTOs;

public record StartLaborSessionRequest(
    string SourceTaskType,
    Guid? SourceTaskId,
    string OperationType,
    Guid? LocationId
);

public record LaborSessionActionResponse(
    Guid SessionId,
    string Status,
    DateTimeOffset StartedAt,
    Guid ShiftId
);

public record CancelLaborSessionRequest(string Reason);

public record LaborSessionDto(
    Guid Id,
    string SourceTaskType,
    Guid? SourceTaskId,
    string ReferenceType,
    Guid? ReferenceId,
    string UserId,
    Guid ShiftId,
    Guid? LocationId,
    Guid? ZoneId,
    string OperationType,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int DurationSeconds,
    int PausedSeconds,
    DateTimeOffset? LastPausedAt,
    DateTimeOffset? TimeoutAt
);

public record LaborSessionEventDto(
    Guid Id,
    string EventType,
    string Actor,
    DateTimeOffset OccurredAt,
    string? Payload,
    string? TraceId
);

public record LaborSessionsQuery(
    string? Status = null,
    string? UserId = null,
    string? OperationType = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public record LaborSessionsResponse(
    List<LaborSessionDto> Items,
    int Total,
    int Page,
    int PageSize
);

public record LaborKpiQuery(
    string? UserId = null,
    Guid? ShiftId = null,
    Guid? ZoneId = null,
    string? OperationType = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null
);

public record LaborKpiSummaryDto(
    int CompletedTaskCount,
    int ActiveSeconds,
    int PausedSeconds,
    double AverageSecondsPerTask,
    double TasksPerHour,
    int IdleSeconds
);

public record LaborKpiGroupDto(
    string Key,
    int CompletedTaskCount,
    int ActiveSeconds,
    double AverageSecondsPerTask,
    double TasksPerHour
);

public record LaborKpiResponse(
    LaborKpiSummaryDto Summary,
    List<LaborKpiGroupDto> GroupByUser,
    List<LaborKpiGroupDto> GroupByShift,
    List<LaborKpiGroupDto> GroupByZone,
    List<LaborKpiGroupDto> GroupByOperation
);

// Advanced Chart DTOs
public record LaborKpiPointDto(string Label, double Value);

public record LaborKpiChartSeriesDto(string Name, List<LaborKpiPointDto> Points);

public record LaborKpiChartResponse(
    List<LaborKpiPointDto> ThroughputTrend,
    List<LaborKpiPointDto> TasksPerHourTrend,
    List<LaborKpiPointDto> OperationMix,
    List<LaborKpiPointDto> UserProductivityRanking,
    List<LaborKpiPointDto> ZoneProductivity
);

public record CurrentShiftResponse(
    Guid ShiftId,
    string ShiftCode,
    DateTimeOffset StartedAt,
    string Status
);
