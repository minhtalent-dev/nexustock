using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Readiness.Contexts;
using Nexustock.Modules.Readiness.Dtos;
using Nexustock.Modules.Readiness.Entities;

namespace Nexustock.Modules.Readiness.Services;

public sealed class CutoverFreezeService : ICutoverFreezeService
{
    private readonly ReadinessDbContext _db;
    private readonly ILogger<CutoverFreezeService> _logger;

    public CutoverFreezeService(ReadinessDbContext db, ILogger<CutoverFreezeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<FreezeStatusResponse> GetStatusAsync(Guid tenantId, CancellationToken ct = default)
    {
        var state = await _db.CutoverFreezeStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (state is null)
            return new FreezeStatusResponse(false, null, null, null);
        return new FreezeStatusResponse(state.IsFrozen, state.FrozenAt, state.FrozenBy, state.Reason);
    }

    public async Task<bool> IsFrozenAsync(Guid tenantId, CancellationToken ct = default)
    {
        var state = await _db.CutoverFreezeStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        return state?.IsFrozen == true;
    }

    public async Task<FreezeStatusResponse> FreezeAsync(Guid tenantId, string actor, string? reason, string? traceId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var state = await _db.CutoverFreezeStates.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (state is null)
        {
            state = new CutoverFreezeState
            {
                TenantId = tenantId,
                CreatedAt = now,
                CreatedBy = actor
            };
            _db.CutoverFreezeStates.Add(state);
        }

        state.IsFrozen = true;
        state.FrozenAt = now;
        state.FrozenBy = actor;
        state.Reason = reason;
        state.UpdatedAt = now;
        state.UpdatedBy = actor;

        _db.CutoverLogs.Add(new CutoverLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StepCode = "FREEZE",
            Status = "Done",
            StartedAt = now,
            EndedAt = now,
            Actor = actor,
            Note = reason ?? "Freeze write APIs",
            TraceId = traceId
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Event={Event} TenantId={TenantId} Actor={Actor} TraceId={TraceId}",
            "readiness.cutover.frozen", tenantId, actor, traceId);

        return new FreezeStatusResponse(true, state.FrozenAt, state.FrozenBy, state.Reason);
    }

    public async Task<FreezeStatusResponse> UnfreezeAsync(Guid tenantId, string actor, string? reason, string? traceId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var state = await _db.CutoverFreezeStates.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (state is null)
        {
            state = new CutoverFreezeState
            {
                TenantId = tenantId,
                IsFrozen = false,
                CreatedAt = now,
                CreatedBy = actor
            };
            _db.CutoverFreezeStates.Add(state);
        }
        else
        {
            state.IsFrozen = false;
            state.UpdatedAt = now;
            state.UpdatedBy = actor;
            state.Reason = reason;
        }

        _db.CutoverLogs.Add(new CutoverLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StepCode = "UNFREEZE",
            Status = "Done",
            StartedAt = now,
            EndedAt = now,
            Actor = actor,
            Note = reason ?? "Unfreeze write APIs",
            TraceId = traceId
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Event={Event} TenantId={TenantId} Actor={Actor} TraceId={TraceId}",
            "readiness.cutover.unfrozen", tenantId, actor, traceId);

        return new FreezeStatusResponse(false, state.FrozenAt, state.FrozenBy, state.Reason);
    }
}
