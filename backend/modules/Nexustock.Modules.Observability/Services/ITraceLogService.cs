using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nexustock.Modules.Observability.Services;

public interface ITraceLogService
{
    Task WriteLogAsync(
        Guid? tenantId,
        string spanName,
        string source,
        string level,
        string message,
        int? durationMs = null,
        object? metadata = null,
        CancellationToken ct = default);
}
