using System;
using System.Threading.Tasks;

namespace Nexustock.Modules.ErpIntegration.Services;

public enum IdempotencyStatus
{
    New,
    Replay,
    Conflict
}

public class IdempotencyResult
{
    public IdempotencyStatus Status { get; set; }
    public string? ResponsePayload { get; set; }
    public string? TraceId { get; set; }
}

public interface IIdempotencyService
{
    Task<IdempotencyResult> CheckIdempotencyAsync(
        Guid tenantId,
        string idempotencyKey,
        string messageType,
        string externalSystem,
        string externalReference,
        string contractVersion,
        string payload,
        string traceId);

    Task SaveResponseAsync(
        Guid tenantId,
        string idempotencyKey,
        string messageType,
        string responsePayload,
        string status,
        string? errorCode = null,
        string? errorMessage = null);
}
