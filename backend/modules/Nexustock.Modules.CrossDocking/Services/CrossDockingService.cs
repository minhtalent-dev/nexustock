using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.CrossDocking.Contexts;
using Nexustock.Modules.CrossDocking.DTOs;
using Nexustock.Modules.CrossDocking.Entities;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Wave.Contexts;

namespace Nexustock.Modules.CrossDocking.Services;

public class CrossDockingException : Exception
{
    public string ErrorCode { get; }
    public int HttpStatus { get; }
    public CrossDockingException(string errorCode, string message, int httpStatus = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        HttpStatus = httpStatus;
    }
}

public class CrossDockingService : ICrossDockingService
{
    private readonly CrossDockingDbContext _db;
    private readonly InboundDbContext _inboundDb;
    private readonly WaveDbContext _waveDb;

    public CrossDockingService(
        CrossDockingDbContext db,
        InboundDbContext inboundDb,
        WaveDbContext waveDb)
    {
        _db = db;
        _inboundDb = inboundDb;
        _waveDb = waveDb;
    }

    public async Task<EvaluateResponse> EvaluateAsync(Guid lotId, Guid tenantId, string actor, CancellationToken ct = default)
    {
        var lot = await _inboundDb.Lots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lotId && l.TenantId == tenantId, ct);

        if (lot is null)
            throw new CrossDockingException("LOT_NOT_FOUND", $"Lot {lotId} not found.", 404);

        if (lot.QcStatus != LotQcStatus.Release)
            throw new CrossDockingException("LOT_NOT_QC_RELEASED", "Lot must have QC status Release to be evaluated for cross-docking.");

        // Find InboundOrderItem for this lot's item to get received qty as available
        var inboundItem = await _inboundDb.InboundOrderItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.ItemId == lot.ItemId)
            .OrderByDescending(i => i.InboundOrderId)
            .FirstOrDefaultAsync(ct);

        var qtyAvailable = inboundItem?.ReceivedQty ?? 0m;

        // Find open WaveItems with unmet demand for the same item
        var openWaveItems = await _waveDb.WaveItems
            .AsNoTracking()
            .Where(wi => wi.TenantId == tenantId && wi.ItemId == lot.ItemId && wi.QtyAllocated < wi.QtyExpected)
            .ToListAsync(ct);

        var candidates = new List<CrossDockCandidate>();

        foreach (var waveItem in openWaveItems)
        {
            var openQty = waveItem.QtyExpected - waveItem.QtyAllocated;
            var qtyMatched = Math.Min(qtyAvailable, openQty);
            if (qtyMatched <= 0) continue;

            var matchScore = (int)Math.Min(100, Math.Floor(qtyMatched / waveItem.QtyExpected * 100));

            var candidate = new CrossDockCandidate
            {
                TenantId = tenantId,
                LotId = lotId,
                InboundOrderItemId = inboundItem?.Id ?? Guid.Empty,
                WaveItemId = waveItem.Id,
                ItemId = lot.ItemId,
                QtyAvailable = qtyAvailable,
                QtyRequested = openQty,
                QtyMatched = qtyMatched,
                MatchScore = matchScore,
                Status = CrossDockCandidateStatus.Pending,
                CreatedBy = actor
            };
            candidates.Add(candidate);

            _db.Candidates.Add(candidate);
            _db.Events.Add(new CrossDockEvent
            {
                TenantId = tenantId,
                CandidateId = candidate.Id,
                EventType = CrossDockEventType.Evaluated,
                Actor = actor,
                Payload = JsonSerializer.Serialize(new { lotId, waveItemId = waveItem.Id, qtyMatched, matchScore })
            });
        }

        await _db.SaveChangesAsync(ct);
        return new EvaluateResponse(candidates.Select(MapToDto).ToList());
    }

    public async Task AcceptAsync(Guid id, Guid tenantId, string actor, CancellationToken ct = default)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        if (candidate is null)
            throw new CrossDockingException("CANDIDATE_NOT_FOUND", $"Candidate {id} not found.", 404);
        if (candidate.Status != CrossDockCandidateStatus.Pending)
            throw new CrossDockingException("CANDIDATE_INVALID_STATUS", "Candidate is not in Pending status.", 409);

        candidate.Status = CrossDockCandidateStatus.Accepted;
        candidate.UpdatedAt = DateTimeOffset.UtcNow;
        candidate.UpdatedBy = actor;

        _db.Events.Add(new CrossDockEvent
        {
            TenantId = tenantId,
            CandidateId = id,
            EventType = CrossDockEventType.Accepted,
            Actor = actor
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(Guid id, string reason, Guid tenantId, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new CrossDockingException("REJECT_REASON_REQUIRED", "Reject reason is required.");

        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        if (candidate is null)
            throw new CrossDockingException("CANDIDATE_NOT_FOUND", $"Candidate {id} not found.", 404);
        if (candidate.Status != CrossDockCandidateStatus.Pending)
            throw new CrossDockingException("CANDIDATE_INVALID_STATUS", "Candidate is not in Pending status.", 409);

        candidate.Status = CrossDockCandidateStatus.Rejected;
        candidate.RejectedReason = reason;
        candidate.UpdatedAt = DateTimeOffset.UtcNow;
        candidate.UpdatedBy = actor;

        _db.Events.Add(new CrossDockEvent
        {
            TenantId = tenantId,
            CandidateId = id,
            EventType = CrossDockEventType.Rejected,
            Actor = actor,
            Payload = JsonSerializer.Serialize(new { reason })
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<CandidateDetailDto?> GetAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var candidate = await _db.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);

        if (candidate is null) return null;

        var events = await _db.Events
            .AsNoTracking()
            .Where(e => e.CandidateId == id)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new EventDto(e.Id, e.EventType.ToString(), e.Actor, e.OccurredAt, e.TraceId))
            .ToListAsync(ct);

        return new CandidateDetailDto(
            candidate.Id, candidate.ItemId, candidate.LotId, candidate.WaveItemId,
            candidate.QtyAvailable, candidate.QtyRequested, candidate.QtyMatched,
            candidate.MatchScore, candidate.Status.ToString(), candidate.RejectedReason,
            candidate.CreatedAt, candidate.CreatedBy, candidate.UpdatedAt, candidate.UpdatedBy,
            events);
    }

    public async Task<PagedResult<CandidateDto>> ListAsync(ListCandidatesQuery query, CancellationToken ct = default)
    {
        var q = _db.Candidates.AsNoTracking().Where(c => c.TenantId == query.TenantId);
        if (query.LotId.HasValue) q = q.Where(c => c.LotId == query.LotId.Value);
        if (query.ItemId.HasValue) q = q.Where(c => c.ItemId == query.ItemId.Value);
        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<CrossDockCandidateStatus>(query.Status, out var status))
            q = q.Where(c => c.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => MapToDto(c))
            .ToListAsync(ct);

        return new PagedResult<CandidateDto>(items, total, query.Page, query.PageSize);
    }

    private static CandidateDto MapToDto(CrossDockCandidate c) =>
        new(c.Id, c.ItemId, c.LotId, c.WaveItemId, c.QtyAvailable, c.QtyRequested, c.QtyMatched, c.MatchScore, c.Status.ToString(), c.CreatedAt);
}
