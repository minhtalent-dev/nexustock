using System;
using System.Threading;
using System.Threading.Tasks;
using Nexustock.Modules.CrossDocking.DTOs;

namespace Nexustock.Modules.CrossDocking.Services;

public interface ICrossDockingService
{
    Task<EvaluateResponse> EvaluateAsync(Guid lotId, Guid tenantId, string actor, CancellationToken ct = default);
    Task AcceptAsync(Guid id, Guid tenantId, string actor, CancellationToken ct = default);
    Task RejectAsync(Guid id, string reason, Guid tenantId, string actor, CancellationToken ct = default);
    Task<CandidateDetailDto?> GetAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<PagedResult<CandidateDto>> ListAsync(ListCandidatesQuery query, CancellationToken ct = default);
}
