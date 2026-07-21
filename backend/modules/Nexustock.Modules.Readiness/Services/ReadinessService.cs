using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Readiness.Contexts;
using Nexustock.Modules.Readiness.Dtos;
using Nexustock.Modules.Readiness.Entities;

namespace Nexustock.Modules.Readiness.Services;

public sealed class ReadinessService : IReadinessService
{
    private static readonly HashSet<string> AllowedScenarios = new(StringComparer.OrdinalIgnoreCase)
    {
        "INBOUND", "QC", "PACK_SCALE", "PRINT_ERROR"
    };

    private static readonly HashSet<string> AllowedDrillScenarios = new(StringComparer.OrdinalIgnoreCase)
    {
        "DB_DOWN", "AGENT_DOWN", "SAP_DOWN"
    };

    private readonly ReadinessDbContext _db;
    private readonly ILogger<ReadinessService> _logger;

    public ReadinessService(ReadinessDbContext db, ILogger<ReadinessService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UatRunListResponse> ListUatRunsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.UatRuns.AsNoTracking().Where(x => x.TenantId == tenantId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

        return new UatRunListResponse(items, total, page, pageSize);
    }

    public async Task<UatRunDto> CreateUatRunAsync(Guid tenantId, CreateUatRunRequest request, string actor, string? traceId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ScenarioCode) || !AllowedScenarios.Contains(request.ScenarioCode))
            throw new InvalidOperationException("READINESS_INVALID_SCENARIO");

        var status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim();
        var entity = new UatRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScenarioCode = request.ScenarioCode.Trim().ToUpperInvariant(),
            Status = status,
            ResultNote = request.ResultNote,
            EvidenceUrl = request.EvidenceUrl,
            TraceId = traceId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor
        };
        _db.UatRuns.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<UatRunDto> SignoffUatRunAsync(Guid tenantId, Guid id, SignoffUatRunRequest request, string actor, string? traceId, CancellationToken ct = default)
    {
        var entity = await _db.UatRuns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct)
            ?? throw new InvalidOperationException("UAT_NOT_FOUND");

        if (!string.Equals(entity.Status, "Passed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("UAT_SIGNOFF_REQUIRED");

        entity.Status = "SignedOff";
        entity.SignedOffBy = actor;
        entity.SignedOffAt = DateTimeOffset.UtcNow;
        entity.ResultNote = request.ResultNote ?? entity.ResultNote;
        entity.EvidenceUrl = request.EvidenceUrl ?? entity.EvidenceUrl;
        entity.TraceId = traceId ?? entity.TraceId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Event={Event} UatRunId={UatRunId} TenantId={TenantId} Actor={Actor} TraceId={TraceId}",
            "readiness.uat.signed_off", entity.Id, tenantId, actor, traceId);

        return ToDto(entity);
    }

    public async Task<CutoverLogListResponse> ListCutoverLogsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.CutoverLogs.AsNoTracking().Where(x => x.TenantId == tenantId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CutoverLogDto(x.Id, x.StepCode, x.Status, x.StartedAt, x.EndedAt, x.Actor, x.Note, x.TraceId))
            .ToListAsync(ct);

        return new CutoverLogListResponse(items, total, page, pageSize);
    }

    public async Task<IncidentDrillDto> CreateIncidentDrillAsync(Guid tenantId, CreateIncidentDrillRequest request, string actor, string? traceId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ScenarioCode) || !AllowedDrillScenarios.Contains(request.ScenarioCode))
            throw new InvalidOperationException("READINESS_INVALID_DRILL_SCENARIO");
        if (request.RtoMinutes <= 0)
            throw new InvalidOperationException("READINESS_INVALID_RTO");

        var entity = new IncidentDrill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScenarioCode = request.ScenarioCode.Trim().ToUpperInvariant(),
            RtoMinutes = request.RtoMinutes,
            Passed = request.Passed,
            ConductedBy = actor,
            ConductedAt = DateTimeOffset.UtcNow,
            EvidenceNote = request.EvidenceNote,
            TraceId = traceId
        };
        _db.IncidentDrills.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new IncidentDrillDto(
            entity.Id, entity.ScenarioCode, entity.RtoMinutes, entity.Passed,
            entity.ConductedBy, entity.ConductedAt, entity.EvidenceNote, entity.TraceId);
    }

    private static UatRunDto ToDto(UatRun x) => new(
        x.Id, x.ScenarioCode, x.Status, x.ResultNote, x.SignedOffBy, x.SignedOffAt,
        x.EvidenceUrl, x.TraceId, x.CreatedAt);
}
