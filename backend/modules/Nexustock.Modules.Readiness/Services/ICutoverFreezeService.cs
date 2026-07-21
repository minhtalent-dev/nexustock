using System;
using System.Threading;
using System.Threading.Tasks;
using Nexustock.Modules.Readiness.Dtos;

namespace Nexustock.Modules.Readiness.Services;

public interface ICutoverFreezeService
{
    Task<FreezeStatusResponse> GetStatusAsync(Guid tenantId, CancellationToken ct = default);
    Task<FreezeStatusResponse> FreezeAsync(Guid tenantId, string actor, string? reason, string? traceId, CancellationToken ct = default);
    Task<FreezeStatusResponse> UnfreezeAsync(Guid tenantId, string actor, string? reason, string? traceId, CancellationToken ct = default);
    Task<bool> IsFrozenAsync(Guid tenantId, CancellationToken ct = default);
}
