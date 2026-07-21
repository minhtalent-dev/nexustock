using System.Threading;
using System.Threading.Tasks;
using Nexustock.Modules.Readiness.Dtos;

namespace Nexustock.Modules.Readiness.Services;

public interface IReadinessProbeService
{
    Task<ReadinessProbeResponse> ProbeAsync(string? traceId, CancellationToken ct = default);
}
