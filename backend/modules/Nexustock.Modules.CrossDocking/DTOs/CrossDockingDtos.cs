using System;
using System.Collections.Generic;

namespace Nexustock.Modules.CrossDocking.DTOs;

public record EvaluateRequest(Guid LotId);

public record RejectRequest(string Reason);

public record CandidateDto(
    Guid Id,
    Guid ItemId,
    Guid LotId,
    Guid WaveItemId,
    decimal QtyAvailable,
    decimal QtyRequested,
    decimal QtyMatched,
    int MatchScore,
    string Status,
    DateTimeOffset CreatedAt
);

public record EventDto(
    Guid Id,
    string EventType,
    string Actor,
    DateTimeOffset OccurredAt,
    string? TraceId
);

public record CandidateDetailDto(
    Guid Id,
    Guid ItemId,
    Guid LotId,
    Guid WaveItemId,
    decimal QtyAvailable,
    decimal QtyRequested,
    decimal QtyMatched,
    int MatchScore,
    string Status,
    string? RejectedReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    List<EventDto> Events
);

public record EvaluateResponse(List<CandidateDto> Candidates);

public record ListCandidatesResponse(List<CandidateDto> Items, int Total, int Page, int PageSize);

public record ListCandidatesQuery(
    Guid TenantId,
    Guid? LotId = null,
    string? Status = null,
    Guid? ItemId = null,
    int Page = 1,
    int PageSize = 20
);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
