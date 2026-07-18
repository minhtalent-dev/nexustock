using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nexustock.Modules.Observability.Services;

public interface IKpiSnapshotService
{
    Task ComputeAndSaveKpisAsync(Guid tenantId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default);
}
