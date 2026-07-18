using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.ErpIntegration.Contexts;
using Nexustock.Modules.ErpIntegration.Entities;

namespace Nexustock.Modules.ErpIntegration.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly ErpIntegrationDbContext _context;
    private readonly IPayloadHashService _hashService;

    public IdempotencyService(ErpIntegrationDbContext context, IPayloadHashService hashService)
    {
        _context = context;
        _hashService = hashService;
    }

    public async Task<IdempotencyResult> CheckIdempotencyAsync(
        Guid tenantId,
        string idempotencyKey,
        string messageType,
        string externalSystem,
        string externalReference,
        string contractVersion,
        string payload,
        string traceId)
    {
        var hash = _hashService.ComputeHash(payload);

        // Explicit IgnoreQueryFilters if needed? No, query filters are scoped to current user tenant.
        // We will query normally, since Multi-Tenant is checked.
        var existingMsg = await _context.IntegrationMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IdempotencyKey == idempotencyKey && m.MessageType == messageType);

        if (existingMsg != null)
        {
            if (existingMsg.PayloadHash == hash)
            {
                return new IdempotencyResult
                {
                    Status = IdempotencyStatus.Replay,
                    ResponsePayload = existingMsg.ResponsePayload,
                    TraceId = existingMsg.TraceId
                };
            }
            else
            {
                // Key matches but payload is different
                existingMsg.UpdatedAt = DateTimeOffset.UtcNow;
                existingMsg.Status = "conflict";
                existingMsg.ErrorCode = "integration.payloadHashMismatch";
                existingMsg.ErrorMessage = "Payload hash mismatch for the same idempotency key.";
                await _context.SaveChangesAsync();

                return new IdempotencyResult
                {
                    Status = IdempotencyStatus.Conflict,
                    TraceId = existingMsg.TraceId
                };
            }
        }

        // Save new message log in "pending/accepted" status
        var newMsg = new IntegrationMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdempotencyKey = idempotencyKey,
            PayloadHash = hash,
            ExternalSystem = externalSystem,
            ExternalReference = externalReference,
            ContractVersion = contractVersion,
            Direction = "inbound",
            MessageType = messageType,
            Payload = payload,
            Status = "accepted",
            TraceId = traceId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.IntegrationMessages.Add(newMsg);
        await _context.SaveChangesAsync();

        return new IdempotencyResult
        {
            Status = IdempotencyStatus.New,
            TraceId = traceId
        };
    }

    public async Task SaveResponseAsync(
        Guid tenantId,
        string idempotencyKey,
        string messageType,
        string responsePayload,
        string status,
        string? errorCode = null,
        string? errorMessage = null)
    {
        var msg = await _context.IntegrationMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IdempotencyKey == idempotencyKey && m.MessageType == messageType);

        if (msg != null)
        {
            msg.ResponsePayload = responsePayload;
            msg.Status = status;
            msg.ErrorCode = errorCode;
            msg.ErrorMessage = errorMessage;
            msg.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
