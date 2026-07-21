using System;
using System.Threading;
using System.Threading.Tasks;
using Nexustock.Modules.Readiness.Dtos;

namespace Nexustock.Modules.Readiness.Services;

public interface IReadinessService
{
    Task<UatRunListResponse> ListUatRunsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<UatRunDto> CreateUatRunAsync(Guid tenantId, CreateUatRunRequest request, string actor, string? traceId, CancellationToken ct = default);
    Task<UatRunDto> SignoffUatRunAsync(Guid tenantId, Guid id, SignoffUatRunRequest request, string actor, string? traceId, CancellationToken ct = default);
    Task<CutoverLogListResponse> ListCutoverLogsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<IncidentDrillDto> CreateIncidentDrillAsync(Guid tenantId, CreateIncidentDrillRequest request, string actor, string? traceId, CancellationToken ct = default);
}
