using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.LaborTracking.Contexts;
using Nexustock.Modules.LaborTracking.DTOs;
using Nexustock.Modules.LaborTracking.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Wave.Contexts;

namespace Nexustock.Modules.LaborTracking.Services;

public class LaborTrackingService : ILaborTrackingService
{
    private readonly LaborTrackingDbContext _dbContext;
    private readonly InventoryDbContext _inventoryDbContext;
    private readonly WaveDbContext _waveDbContext;
    private readonly MasterDataDbContext _masterDataDbContext;

    public LaborTrackingService(
        LaborTrackingDbContext dbContext,
        InventoryDbContext inventoryDbContext,
        WaveDbContext waveDbContext,
        MasterDataDbContext masterDataDbContext)
    {
        _dbContext = dbContext;
        _inventoryDbContext = inventoryDbContext;
        _waveDbContext = waveDbContext;
        _masterDataDbContext = masterDataDbContext;
    }

    public async Task<LaborSessionActionResponse> StartAsync(StartLaborSessionRequest request, Guid tenantId, string actor, CancellationToken ct)
    {
        // 1. Kiểm tra session đang hoạt động
        var hasActive = await _dbContext.LaborSessions
            .AnyAsync(x => x.TenantId == tenantId && x.UserId == actor && (x.Status == "Running" || x.Status == "Paused"), ct);
        if (hasActive)
        {
            throw new InvalidOperationException("LABOR_SESSION_ALREADY_ACTIVE");
        }

        // 2. Resolve source task info
        Guid? resolvedLocationId = request.LocationId;
        string referenceType = "Manual";
        Guid? referenceId = null;

        if (!string.Equals(request.SourceTaskType, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            if (request.SourceTaskId == null)
            {
                throw new ArgumentException("LABOR_SOURCE_TASK_INVALID");
            }

            if (string.Equals(request.SourceTaskType, "MobileTask", StringComparison.OrdinalIgnoreCase))
            {
                var task = await _inventoryDbContext.MobileTasks.FirstOrDefaultAsync(x => x.Id == request.SourceTaskId && x.TenantId == tenantId, ct);
                if (task == null) throw new KeyNotFoundException("LABOR_SOURCE_TASK_NOT_FOUND");
                resolvedLocationId = task.LocationId;
                referenceType = task.ReferenceType;
                referenceId = task.ReferenceId;
            }
            else if (string.Equals(request.SourceTaskType, "PickTask", StringComparison.OrdinalIgnoreCase))
            {
                var task = await _inventoryDbContext.PickTasks.FirstOrDefaultAsync(x => x.Id == request.SourceTaskId && x.TenantId == tenantId, ct);
                if (task == null) throw new KeyNotFoundException("LABOR_SOURCE_TASK_NOT_FOUND");
                resolvedLocationId = task.FromLocationId;
                referenceType = "Shipment";
                referenceId = task.ShipmentId;
            }
            else if (string.Equals(request.SourceTaskType, "WavePickTask", StringComparison.OrdinalIgnoreCase))
            {
                var task = await _waveDbContext.WavePickTasks.FirstOrDefaultAsync(x => x.Id == request.SourceTaskId && x.TenantId == tenantId, ct);
                if (task == null) throw new KeyNotFoundException("LABOR_SOURCE_TASK_NOT_FOUND");
                resolvedLocationId = task.FromLocationId;
                referenceType = "Wave";
                referenceId = task.WaveId;
            }
            else
            {
                throw new ArgumentException("LABOR_SOURCE_TASK_INVALID");
            }
        }

        // 3. Resolve ZoneId
        Guid? resolvedZoneId = null;
        if (resolvedLocationId != null)
        {
            var loc = await _masterDataDbContext.StorageLocations.FirstOrDefaultAsync(x => x.Id == resolvedLocationId && x.TenantId == tenantId, ct);
            if (loc != null)
            {
                resolvedZoneId = loc.ZoneId;
            }
        }

        // 4. Lấy hoặc tự động tạo ca làm việc (Shift)
        var shift = await GetOrCreateActiveShiftAsync(actor, tenantId, ct);

        // 5. Tạo session mới
        var now = DateTimeOffset.UtcNow;
        var session = new LaborSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceTaskType = request.SourceTaskType,
            SourceTaskId = request.SourceTaskId,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            UserId = actor,
            ShiftId = shift.Id,
            LocationId = resolvedLocationId,
            ZoneId = resolvedZoneId,
            OperationType = request.OperationType,
            Status = "Running",
            StartedAt = now,
            DurationSeconds = 0,
            PausedSeconds = 0,
            TimeoutAt = now.AddHours(8), // SLA timeout 8h mặc định
            CreatedAt = now,
            CreatedBy = actor
        };

        _dbContext.LaborSessions.Add(session);

        // 6. Ghi event Started
        var startedEvent = new LaborSessionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            EventType = "Started",
            Actor = actor,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new { session.SourceTaskType, session.SourceTaskId, session.OperationType })
        };
        _dbContext.LaborSessionEvents.Add(startedEvent);

        await _dbContext.SaveChangesAsync(ct);

        return new LaborSessionActionResponse(session.Id, session.Status, session.StartedAt, session.ShiftId);
    }

    public async Task<LaborSessionDto> PauseAsync(Guid id, Guid tenantId, string actor, string? traceId, CancellationToken ct)
    {
        var session = await _dbContext.LaborSessions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (session == null) throw new KeyNotFoundException("LABOR_SESSION_NOT_FOUND");
        if (session.Status != "Running") throw new InvalidOperationException("LABOR_SESSION_INVALID_STATUS");

        var now = DateTimeOffset.UtcNow;
        session.Status = "Paused";
        session.LastPausedAt = now;
        session.UpdatedAt = now;
        session.UpdatedBy = actor;

        var pauseEvent = new LaborSessionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            EventType = "Paused",
            Actor = actor,
            OccurredAt = now,
            TraceId = traceId
        };
        _dbContext.LaborSessionEvents.Add(pauseEvent);

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(session);
    }

    public async Task<LaborSessionDto> ResumeAsync(Guid id, Guid tenantId, string actor, string? traceId, CancellationToken ct)
    {
        var session = await _dbContext.LaborSessions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (session == null) throw new KeyNotFoundException("LABOR_SESSION_NOT_FOUND");
        if (session.Status != "Paused") throw new InvalidOperationException("LABOR_SESSION_INVALID_STATUS");

        var now = DateTimeOffset.UtcNow;
        if (session.LastPausedAt != null)
        {
            var delta = (int)(now - session.LastPausedAt.Value).TotalSeconds;
            if (delta < 0) throw new InvalidOperationException("LABOR_DURATION_INVALID");
            session.PausedSeconds += delta;
        }
        session.Status = "Running";
        session.LastPausedAt = null;
        session.UpdatedAt = now;
        session.UpdatedBy = actor;

        var resumeEvent = new LaborSessionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            EventType = "Resumed",
            Actor = actor,
            OccurredAt = now,
            TraceId = traceId
        };
        _dbContext.LaborSessionEvents.Add(resumeEvent);

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(session);
    }

    public async Task<LaborSessionDto> CompleteAsync(Guid id, Guid tenantId, string actor, string? traceId, CancellationToken ct)
    {
        var session = await _dbContext.LaborSessions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (session == null) throw new KeyNotFoundException("LABOR_SESSION_NOT_FOUND");
        if (session.Status != "Running" && session.Status != "Paused")
        {
            throw new InvalidOperationException("LABOR_SESSION_INVALID_STATUS");
        }

        var now = DateTimeOffset.UtcNow;
        if (session.Status == "Paused" && session.LastPausedAt != null)
        {
            var delta = (int)(now - session.LastPausedAt.Value).TotalSeconds;
            if (delta >= 0) session.PausedSeconds += delta;
            session.LastPausedAt = null;
        }

        var totalSeconds = (int)(now - session.StartedAt).TotalSeconds;
        var duration = totalSeconds - session.PausedSeconds;
        if (duration < 0) throw new InvalidOperationException("LABOR_DURATION_INVALID");

        session.Status = "Completed";
        session.CompletedAt = now;
        session.DurationSeconds = duration;
        session.UpdatedAt = now;
        session.UpdatedBy = actor;

        var completeEvent = new LaborSessionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            EventType = "Completed",
            Actor = actor,
            OccurredAt = now,
            TraceId = traceId
        };
        _dbContext.LaborSessionEvents.Add(completeEvent);

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(session);
    }

    public async Task<LaborSessionDto> CancelAsync(Guid id, string reason, Guid tenantId, string actor, string? traceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("LABOR_CANCEL_REASON_REQUIRED");
        }

        var session = await _dbContext.LaborSessions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (session == null) throw new KeyNotFoundException("LABOR_SESSION_NOT_FOUND");
        if (session.Status != "Running" && session.Status != "Paused")
        {
            throw new InvalidOperationException("LABOR_SESSION_INVALID_STATUS");
        }

        var now = DateTimeOffset.UtcNow;
        session.Status = "Cancelled";
        session.CompletedAt = now;
        session.UpdatedAt = now;
        session.UpdatedBy = actor;

        var cancelEvent = new LaborSessionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            EventType = "Cancelled",
            Actor = actor,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new { reason }),
            TraceId = traceId
        };
        _dbContext.LaborSessionEvents.Add(cancelEvent);

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(session);
    }

    public async Task<LaborSessionsResponse> ListAsync(LaborSessionsQuery query, Guid tenantId, CancellationToken ct)
    {
        var dbQuery = _dbContext.LaborSessions.Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query.Status))
        {
            dbQuery = dbQuery.Where(x => x.Status == query.Status);
        }
        if (!string.IsNullOrEmpty(query.UserId))
        {
            dbQuery = dbQuery.Where(x => x.UserId == query.UserId);
        }
        if (!string.IsNullOrEmpty(query.OperationType))
        {
            dbQuery = dbQuery.Where(x => x.OperationType == query.OperationType);
        }
        if (query.FromDate != null)
        {
            dbQuery = dbQuery.Where(x => x.StartedAt >= query.FromDate.Value);
        }
        if (query.ToDate != null)
        {
            dbQuery = dbQuery.Where(x => x.StartedAt <= query.ToDate.Value);
        }

        var total = await dbQuery.CountAsync(ct);
        var items = await dbQuery
            .OrderByDescending(x => x.StartedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => MapToDto(x))
            .ToListAsync(ct);

        return new LaborSessionsResponse(items, total, query.Page, query.PageSize);
    }

    public async Task<LaborKpiResponse> GetKpiAsync(LaborKpiQuery query, Guid tenantId, CancellationToken ct)
    {
        var dbQuery = _dbContext.LaborSessions.Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query.UserId))
        {
            dbQuery = dbQuery.Where(x => x.UserId == query.UserId);
        }
        if (query.ShiftId != null)
        {
            dbQuery = dbQuery.Where(x => x.ShiftId == query.ShiftId.Value);
        }
        if (query.ZoneId != null)
        {
            dbQuery = dbQuery.Where(x => x.ZoneId == query.ZoneId.Value);
        }
        if (!string.IsNullOrEmpty(query.OperationType))
        {
            dbQuery = dbQuery.Where(x => x.OperationType == query.OperationType);
        }
        if (query.FromDate != null)
        {
            dbQuery = dbQuery.Where(x => x.StartedAt >= query.FromDate.Value);
        }
        if (query.ToDate != null)
        {
            dbQuery = dbQuery.Where(x => x.StartedAt <= query.ToDate.Value);
        }

        var sessions = await dbQuery.ToListAsync(ct);
        var completedSessions = sessions.Where(x => x.Status == "Completed").ToList();

        int completedCount = completedSessions.Count;
        int activeSecs = completedSessions.Sum(x => x.DurationSeconds);
        int pausedSecs = completedSessions.Sum(x => x.PausedSeconds);

        double avgSecs = completedCount > 0 ? (double)activeSecs / completedCount : 0.0;
        double tasksHr = activeSecs > 0 ? (double)completedCount / ((double)activeSecs / 3600.0) : 0.0;

        // Giả lập shift elapsed time nếu có shiftId cụ thể
        int idleSecs = 0;
        if (query.ShiftId != null)
        {
            var shift = await _dbContext.LaborShifts.FirstOrDefaultAsync(x => x.Id == query.ShiftId.Value && x.TenantId == tenantId, ct);
            if (shift != null)
            {
                var end = shift.EndedAt ?? DateTimeOffset.UtcNow;
                var totalShiftSecs = (int)(end - shift.StartedAt).TotalSeconds;
                idleSecs = totalShiftSecs - activeSecs - pausedSecs;
                if (idleSecs < 0) idleSecs = 0;
            }
        }

        var summary = new LaborKpiSummaryDto(completedCount, activeSecs, pausedSecs, avgSecs, tasksHr, idleSecs);

        // Groupings
        var byUser = completedSessions.GroupBy(x => x.UserId)
            .Select(g => CreateGroupDto(g.Key, g.ToList()))
            .ToList();

        var byShift = completedSessions.GroupBy(x => x.ShiftId.ToString())
            .Select(g => CreateGroupDto(g.Key, g.ToList()))
            .ToList();

        var byZone = completedSessions.GroupBy(x => x.ZoneId?.ToString() ?? "Unassigned")
            .Select(g => CreateGroupDto(g.Key, g.ToList()))
            .ToList();

        var byOperation = completedSessions.GroupBy(x => x.OperationType)
            .Select(g => CreateGroupDto(g.Key, g.ToList()))
            .ToList();

        return new LaborKpiResponse(summary, byUser, byShift, byZone, byOperation);
    }

    public async Task<LaborKpiChartResponse> GetKpiChartsAsync(LaborKpiQuery query, Guid tenantId, CancellationToken ct)
    {
        var dbQuery = _dbContext.LaborSessions.Where(x => x.TenantId == tenantId && x.Status == "Completed");

        if (!string.IsNullOrEmpty(query.UserId))
        {
            dbQuery = dbQuery.Where(x => x.UserId == query.UserId);
        }
        if (query.ShiftId != null)
        {
            dbQuery = dbQuery.Where(x => x.ShiftId == query.ShiftId.Value);
        }
        if (query.ZoneId != null)
        {
            dbQuery = dbQuery.Where(x => x.ZoneId == query.ZoneId.Value);
        }
        if (!string.IsNullOrEmpty(query.OperationType))
        {
            dbQuery = dbQuery.Where(x => x.OperationType == query.OperationType);
        }
        if (query.FromDate != null)
        {
            dbQuery = dbQuery.Where(x => x.StartedAt >= query.FromDate.Value);
        }
        if (query.ToDate != null)
        {
            dbQuery = dbQuery.Where(x => x.StartedAt <= query.ToDate.Value);
        }

        var completed = await dbQuery.ToListAsync(ct);

        // 1. Throughput trend (Group by Day/Hour)
        var throughputTrend = completed
            .GroupBy(x => x.StartedAt.ToString("yyyy-MM-dd HH:00"))
            .OrderBy(g => g.Key)
            .Select(g => new LaborKpiPointDto(g.Key, g.Count()))
            .ToList();

        // 2. Tasks/Hour trend
        var tasksPerHourTrend = completed
            .GroupBy(x => x.StartedAt.ToString("yyyy-MM-dd HH:00"))
            .OrderBy(g => g.Key)
            .Select(g => {
                var count = g.Count();
                var active = g.Sum(x => x.DurationSeconds);
                double tph = active > 0 ? (double)count / ((double)active / 3600.0) : 0.0;
                return new LaborKpiPointDto(g.Key, Math.Round(tph, 2));
            })
            .ToList();

        // 3. Operation Mix
        var operationMix = completed
            .GroupBy(x => x.OperationType)
            .Select(g => new LaborKpiPointDto(g.Key, g.Count()))
            .ToList();

        // 4. User productivity ranking (Top TPH)
        var userProductivityRanking = completed
            .GroupBy(x => x.UserId)
            .Select(g => {
                var count = g.Count();
                var active = g.Sum(x => x.DurationSeconds);
                double tph = active > 0 ? (double)count / ((double)active / 3600.0) : 0.0;
                return new LaborKpiPointDto(g.Key, Math.Round(tph, 2));
            })
            .OrderByDescending(x => x.Value)
            .Take(10)
            .ToList();

        // 5. Zone productivity
        var zoneProductivity = completed
            .Where(x => x.ZoneId != null)
            .GroupBy(x => x.ZoneId!.ToString())
            .Select(g => {
                var count = g.Count();
                var active = g.Sum(x => x.DurationSeconds);
                double avgSecs = count > 0 ? (double)active / count : 0.0;
                return new LaborKpiPointDto(g.Key, Math.Round(avgSecs, 1));
            })
            .ToList();

        return new LaborKpiChartResponse(
            throughputTrend,
            tasksPerHourTrend,
            operationMix,
            userProductivityRanking,
            zoneProductivity
        );
    }

    public async Task<CurrentShiftResponse> GetCurrentShiftAsync(string userId, Guid tenantId, CancellationToken ct)
    {
        var shift = await _dbContext.LaborShifts
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.Status == "Open", ct);

        if (shift == null)
        {
            shift = await GetOrCreateActiveShiftAsync(userId, tenantId, ct);
        }

        return new CurrentShiftResponse(shift.Id, shift.ShiftCode, shift.StartedAt, shift.Status);
    }

    private async Task<LaborShift> GetOrCreateActiveShiftAsync(string userId, Guid tenantId, CancellationToken ct)
    {
        var shift = await _dbContext.LaborShifts
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.Status == "Open", ct);

        if (shift == null)
        {
            var now = DateTimeOffset.UtcNow;
            shift = new LaborShift
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                ShiftCode = $"SHIFT-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                StartedAt = now,
                Status = "Open",
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.LaborShifts.Add(shift);
            await _dbContext.SaveChangesAsync(ct);
        }
        return shift;
    }

    private static LaborSessionDto MapToDto(LaborSession x)
    {
        return new LaborSessionDto(
            x.Id,
            x.SourceTaskType,
            x.SourceTaskId,
            x.ReferenceType,
            x.ReferenceId,
            x.UserId,
            x.ShiftId,
            x.LocationId,
            x.ZoneId,
            x.OperationType,
            x.Status,
            x.StartedAt,
            x.CompletedAt,
            x.DurationSeconds,
            x.PausedSeconds,
            x.LastPausedAt,
            x.TimeoutAt
        );
    }

    private static LaborKpiGroupDto CreateGroupDto(string key, List<LaborSession> completedList)
    {
        int count = completedList.Count;
        int active = completedList.Sum(x => x.DurationSeconds);
        double avg = count > 0 ? (double)active / count : 0.0;
        double tph = active > 0 ? (double)count / ((double)active / 3600.0) : 0.0;
        return new LaborKpiGroupDto(key, count, active, avg, tph);
    }
}
