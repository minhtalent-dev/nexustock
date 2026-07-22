using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nexustock.Modules.Qc.Abstractions;

public interface IQcGateService
{
    Task EnsureLotUsableAsync(Guid tenantId, Guid lotId, CancellationToken ct = default);

    Task EnsureLotUsableByLotNoAsync(Guid tenantId, Guid itemId, string lotNo, CancellationToken ct = default);
}
