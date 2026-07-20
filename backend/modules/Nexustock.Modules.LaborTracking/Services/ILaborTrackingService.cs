using System;
using System.Threading;
using System.Threading.Tasks;
using Nexustock.Modules.LaborTracking.DTOs;

namespace Nexustock.Modules.LaborTracking.Services;

public interface ILaborTrackingService
{
    Task<LaborSessionActionResponse> StartAsync(StartLaborSessionRequest request, Guid tenantId, string actor, CancellationToken ct);
    Task<LaborSessionDto> PauseAsync(Guid id, Guid tenantId, string actor, string? traceId, CancellationToken ct);
    Task<LaborSessionDto> ResumeAsync(Guid id, Guid tenantId, string actor, string? traceId, CancellationToken ct);
    Task<LaborSessionDto> CompleteAsync(Guid id, Guid tenantId, string actor, string? traceId, CancellationToken ct);
    Task<LaborSessionDto> CancelAsync(Guid id, string reason, Guid tenantId, string actor, string? traceId, CancellationToken ct);
    Task<LaborSessionsResponse> ListAsync(LaborSessionsQuery query, Guid tenantId, CancellationToken ct);
    Task<LaborKpiResponse> GetKpiAsync(LaborKpiQuery query, Guid tenantId, CancellationToken ct);
    Task<LaborKpiChartResponse> GetKpiChartsAsync(LaborKpiQuery query, Guid tenantId, CancellationToken ct);
    Task<CurrentShiftResponse> GetCurrentShiftAsync(string userId, Guid tenantId, CancellationToken ct);
}
