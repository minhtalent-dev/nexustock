using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Readiness.Dtos;

public record ProbeComponentDto(string Name, string Status, string? Detail);

public record ReadinessProbeResponse(
    string OverallStatus,
    IReadOnlyList<ProbeComponentDto> Components,
    string? TraceId);

public record UatRunDto(
    Guid Id,
    string ScenarioCode,
    string Status,
    string? ResultNote,
    string? SignedOffBy,
    DateTimeOffset? SignedOffAt,
    string? EvidenceUrl,
    string? TraceId,
    DateTimeOffset CreatedAt);

public record UatRunListResponse(IReadOnlyList<UatRunDto> Items, int Total, int Page, int PageSize);

public record CreateUatRunRequest(
    string ScenarioCode,
    string Status,
    string? ResultNote,
    string? EvidenceUrl);

public record SignoffUatRunRequest(string? ResultNote, string? EvidenceUrl);

public record CutoverLogDto(
    Guid Id,
    string StepCode,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string Actor,
    string? Note,
    string? TraceId);

public record CutoverLogListResponse(IReadOnlyList<CutoverLogDto> Items, int Total, int Page, int PageSize);

public record FreezeRequest(string? Reason);

public record FreezeStatusResponse(bool IsFrozen, DateTimeOffset? FrozenAt, string? FrozenBy, string? Reason);

public record CreateIncidentDrillRequest(
    string ScenarioCode,
    int RtoMinutes,
    bool Passed,
    string? EvidenceNote);

public record IncidentDrillDto(
    Guid Id,
    string ScenarioCode,
    int RtoMinutes,
    bool Passed,
    string ConductedBy,
    DateTimeOffset ConductedAt,
    string? EvidenceNote,
    string? TraceId);
