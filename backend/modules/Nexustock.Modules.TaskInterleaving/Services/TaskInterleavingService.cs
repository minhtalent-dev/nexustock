using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.LaborTracking.Contexts;
using Nexustock.Modules.TaskInterleaving.Contexts;
using Nexustock.Modules.TaskInterleaving.Dtos;
using Nexustock.Modules.TaskInterleaving.Entities;

namespace Nexustock.Modules.TaskInterleaving.Services;

public class TaskInterleavingService : ITaskInterleavingService
{
    private readonly TaskInterleavingDbContext _context;
    private readonly InventoryDbContext _inventoryContext;
    private readonly MasterDataDbContext _masterContext;
    private readonly LaborTrackingDbContext _laborContext;
    private readonly ILogger<TaskInterleavingService> _logger;

    private static readonly HashSet<string> AllowedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Picking", "Putaway", "Replenishment", "CycleCount", "Packing", "Receiving"
    };

    public TaskInterleavingService(
        TaskInterleavingDbContext context,
        InventoryDbContext inventoryContext,
        MasterDataDbContext masterContext,
        LaborTrackingDbContext laborContext,
        ILogger<TaskInterleavingService> logger)
    {
        _context = context;
        _inventoryContext = inventoryContext;
        _masterContext = masterContext;
        _laborContext = laborContext;
        _logger = logger;
    }

    public async Task<NextTaskRecommendationResponse> GetNextAsync(
        NextTaskRecommendationQuery query, Guid tenantId, Guid userId, string actor, string traceId, CancellationToken ct)
    {
        var userIdStr = userId.ToString();
        var activeSession = await _laborContext.LaborSessions
            .Where(s => s.TenantId == tenantId && s.UserId == userIdStr && s.Status == "Running")
            .FirstOrDefaultAsync(ct);

        await SupersedeOpenRecommendationsAsync(tenantId, userId, actor, traceId, ct);

        var openTasksQuery = _inventoryContext.MobileTasks
            .Where(t => t.TenantId == tenantId && t.Status == "Open" && t.AssignedUser == null);

        if (!string.IsNullOrWhiteSpace(query.OperationType))
        {
            var opType = query.OperationType.Trim();
            if (!AllowedOperations.Contains(opType))
            {
                return await CreateNoCandidateResponse(tenantId, userId, activeSession, query, traceId, actor, ct);
            }
            openTasksQuery = openTasksQuery.Where(t => t.ReferenceType == opType);
        }
        else
        {
            openTasksQuery = openTasksQuery.Where(t => AllowedOperations.Contains(t.ReferenceType));
        }

        var openTasks = await openTasksQuery.ToListAsync(ct);

        var recommendation = new TaskRecommendation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ShiftId = activeSession?.ShiftId,
            LaborSessionId = activeSession?.Id,
            SourceTaskType = query.SourceTaskType,
            SourceTaskId = query.SourceTaskId,
            CurrentLocationId = query.CurrentLocationId ?? activeSession?.LocationId,
            CurrentZoneId = query.CurrentZoneId ?? activeSession?.ZoneId,
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            ExpiresAt = DateTime.UtcNow.AddSeconds(120)
        };

        if (openTasks.Count == 0)
        {
            recommendation.Status = "NoCandidate";
            recommendation.ReasonCode = "NO_ELIGIBLE_TASK";
            _context.TaskRecommendations.Add(recommendation);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "event={Event} recommendationId={RecommendationId} tenantId={TenantId} userId={UserId} traceId={TraceId} status={Status}",
                "task_interleaving.recommendation.no_candidate", recommendation.Id, tenantId, userId, traceId, recommendation.Status);

            return new NextTaskRecommendationResponse
            {
                RecommendationId = recommendation.Id,
                Status = "NoCandidate",
                TraceId = traceId
            };
        }

        var targetLocationIds = openTasks.Where(t => t.LocationId.HasValue).Select(t => t.LocationId!.Value).Distinct().ToList();
        var currentLocId = recommendation.CurrentLocationId;
        if (currentLocId.HasValue && !targetLocationIds.Contains(currentLocId.Value))
        {
            targetLocationIds.Add(currentLocId.Value);
        }

        var locations = await _masterContext.StorageLocations
            .Where(l => l.TenantId == tenantId && targetLocationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l, ct);

        var currentLoc = currentLocId.HasValue && locations.ContainsKey(currentLocId.Value) ? locations[currentLocId.Value] : null;
        var currentZoneId = recommendation.CurrentZoneId ?? currentLoc?.ZoneId;
        var operationContext = !string.IsNullOrWhiteSpace(query.OperationType)
            ? query.OperationType.Trim()
            : activeSession?.OperationType;

        var candidatesList = new List<TaskRecommendationCandidate>();

        foreach (var task in openTasks)
        {
            var candidateLoc = task.LocationId.HasValue && locations.ContainsKey(task.LocationId.Value)
                ? locations[task.LocationId.Value]
                : null;

            var ageSeconds = (int)(DateTime.UtcNow - task.CreatedAt).TotalSeconds;
            var score = TaskInterleavingScorer.Score(new TaskInterleavingScorer.ScoreInput
            {
                CurrentLocationId = currentLoc?.Id ?? currentLocId,
                CurrentZoneId = currentZoneId,
                CandidateLocationId = task.LocationId,
                CandidateZoneId = candidateLoc?.ZoneId,
                AgeSeconds = ageSeconds,
                Step = task.Step,
                HasActiveSession = activeSession != null,
                SameOperation = !string.IsNullOrEmpty(operationContext)
                    && string.Equals(task.ReferenceType, operationContext, StringComparison.OrdinalIgnoreCase),
                SameZoneAsContext = currentZoneId.HasValue && candidateLoc?.ZoneId == currentZoneId,
                IsConflictRisk = task.Status != "Open" || task.AssignedUser != null
            });

            var explanation = new TaskScoreExplanationDto
            {
                DistanceScore = score.DistanceScore,
                AgeScore = score.AgeScore,
                PriorityScore = score.PriorityScore,
                ContinuityScore = score.ContinuityScore,
                PenaltyScore = score.PenaltyScore
            };

            candidatesList.Add(new TaskRecommendationCandidate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RecommendationId = recommendation.Id,
                TaskType = "MobileTask",
                TaskId = task.Id,
                OperationType = task.ReferenceType,
                LocationId = task.LocationId,
                ZoneId = candidateLoc?.ZoneId,
                TaskStatus = task.Status,
                Priority = task.Step?.ToUpperInvariant() == "HIGH" ? 2 : task.Step?.ToUpperInvariant() == "MEDIUM" ? 1 : 0,
                AgeSeconds = ageSeconds,
                DistanceScore = score.DistanceScore,
                AgeScore = score.AgeScore,
                PriorityScore = score.PriorityScore,
                ContinuityScore = score.ContinuityScore,
                PenaltyScore = score.PenaltyScore,
                TotalScore = score.TotalScore,
                Explanation = JsonSerializer.Serialize(explanation),
                CreatedAt = DateTime.UtcNow
            });
        }

        var sortedCandidates = candidatesList
            .OrderByDescending(c => c.TotalScore)
            .ThenByDescending(c => c.PriorityScore)
            .ThenByDescending(c => c.AgeSeconds)
            .ThenBy(c => c.TaskId)
            .ToList();

        var selected = sortedCandidates.First();

        recommendation.Status = "Open";
        recommendation.SelectedTaskType = selected.TaskType;
        recommendation.SelectedTaskId = selected.TaskId;
        recommendation.SelectedScore = selected.TotalScore;
        recommendation.CurrentZoneId = currentZoneId;

        _context.TaskRecommendations.Add(recommendation);
        _context.TaskRecommendationCandidates.AddRange(sortedCandidates);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "event={Event} recommendationId={RecommendationId} tenantId={TenantId} userId={UserId} traceId={TraceId} status={Status} selectedTaskId={SelectedTaskId}",
            "task_interleaving.recommendation.created", recommendation.Id, tenantId, userId, traceId, recommendation.Status, selected.TaskId);

        var maxCandidates = Math.Clamp(query.MaxCandidates <= 0 ? 10 : query.MaxCandidates, 1, 25);
        var topCandidates = sortedCandidates.Take(maxCandidates).Select(c => new TaskRecommendationCandidateDto
        {
            TaskType = c.TaskType,
            TaskId = c.TaskId,
            OperationType = c.OperationType,
            LocationId = c.LocationId,
            ZoneId = c.ZoneId,
            Score = c.TotalScore,
            Explanation = JsonSerializer.Deserialize<TaskScoreExplanationDto>(c.Explanation) ?? new()
        }).ToList();

        return new NextTaskRecommendationResponse
        {
            RecommendationId = recommendation.Id,
            Status = recommendation.Status,
            ExpiresAt = recommendation.ExpiresAt,
            Selected = topCandidates.First(),
            Candidates = topCandidates,
            TraceId = traceId
        };
    }

    private async Task SupersedeOpenRecommendationsAsync(
        Guid tenantId, Guid userId, string actor, string traceId, CancellationToken ct)
    {
        var openRecs = await _context.TaskRecommendations
            .Where(r => r.TenantId == tenantId && r.UserId == userId && r.Status == "Open")
            .ToListAsync(ct);

        if (openRecs.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var rec in openRecs)
        {
            if (rec.ExpiresAt < now)
            {
                rec.Status = "Expired";
                rec.ReasonCode = "TASK_EXPIRED";
                _logger.LogInformation(
                    "event={Event} recommendationId={RecommendationId} tenantId={TenantId} userId={UserId} traceId={TraceId} status={Status}",
                    "task_interleaving.recommendation.expired", rec.Id, tenantId, userId, traceId, rec.Status);
            }
            else
            {
                rec.Status = "Superseded";
                rec.UpdatedAt = now;
                rec.UpdatedBy = actor;
            }
            rec.UpdatedAt = now;
            rec.UpdatedBy = actor;
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task<NextTaskRecommendationResponse> CreateNoCandidateResponse(
        Guid tenantId, Guid userId, LaborTracking.Entities.LaborSession? activeSession,
        NextTaskRecommendationQuery query, string traceId, string actor, CancellationToken ct)
    {
        await SupersedeOpenRecommendationsAsync(tenantId, userId, actor, traceId, ct);

        var recommendation = new TaskRecommendation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ShiftId = activeSession?.ShiftId,
            LaborSessionId = activeSession?.Id,
            SourceTaskType = query.SourceTaskType,
            SourceTaskId = query.SourceTaskId,
            CurrentLocationId = query.CurrentLocationId ?? activeSession?.LocationId,
            CurrentZoneId = query.CurrentZoneId ?? activeSession?.ZoneId,
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            ExpiresAt = DateTime.UtcNow.AddSeconds(120),
            Status = "NoCandidate",
            ReasonCode = "NO_ELIGIBLE_TASK"
        };
        _context.TaskRecommendations.Add(recommendation);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "event={Event} recommendationId={RecommendationId} tenantId={TenantId} userId={UserId} traceId={TraceId} status={Status}",
            "task_interleaving.recommendation.no_candidate", recommendation.Id, tenantId, userId, traceId, recommendation.Status);

        return new NextTaskRecommendationResponse
        {
            RecommendationId = recommendation.Id,
            Status = "NoCandidate",
            TraceId = traceId
        };
    }

    public async Task<TaskRecommendationDetailResponse> GetDetailAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var rec = await _context.TaskRecommendations
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);

        if (rec == null)
        {
            throw new KeyNotFoundException("TASK_RECOMMENDATION_NOT_FOUND");
        }

        var candidates = await _context.TaskRecommendationCandidates
            .Where(c => c.TenantId == tenantId && c.RecommendationId == id)
            .OrderByDescending(c => c.TotalScore)
            .ToListAsync(ct);

        return new TaskRecommendationDetailResponse
        {
            Id = rec.Id,
            UserId = rec.UserId,
            ShiftId = rec.ShiftId,
            LaborSessionId = rec.LaborSessionId,
            SourceTaskType = rec.SourceTaskType,
            SourceTaskId = rec.SourceTaskId,
            CurrentLocationId = rec.CurrentLocationId,
            CurrentZoneId = rec.CurrentZoneId,
            Status = rec.Status,
            SelectedTaskType = rec.SelectedTaskType,
            SelectedTaskId = rec.SelectedTaskId,
            SelectedScore = rec.SelectedScore,
            ReasonCode = rec.ReasonCode,
            DecisionNote = rec.DecisionNote,
            AcceptedAt = rec.AcceptedAt,
            RejectedAt = rec.RejectedAt,
            ExpiresAt = rec.ExpiresAt,
            TraceId = rec.TraceId,
            CreatedAt = rec.CreatedAt,
            CreatedBy = rec.CreatedBy,
            Candidates = candidates.Select(c => new TaskRecommendationCandidateDto
            {
                TaskType = c.TaskType,
                TaskId = c.TaskId,
                OperationType = c.OperationType,
                LocationId = c.LocationId,
                ZoneId = c.ZoneId,
                Score = c.TotalScore,
                Explanation = JsonSerializer.Deserialize<TaskScoreExplanationDto>(c.Explanation) ?? new()
            }).ToList()
        };
    }

    public async Task<PagedResult<TaskRecommendationListItemDto>> ListAsync(TaskRecommendationListQuery query, Guid tenantId, CancellationToken ct)
    {
        var dbQuery = _context.TaskRecommendations.Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query.Status))
            dbQuery = dbQuery.Where(r => r.Status == query.Status);
        if (query.UserId.HasValue)
            dbQuery = dbQuery.Where(r => r.UserId == query.UserId.Value);
        if (!string.IsNullOrEmpty(query.OperationType))
        {
            var opType = query.OperationType.Trim();
            dbQuery = dbQuery.Where(r => _context.TaskRecommendationCandidates.Any(c =>
                c.TenantId == tenantId &&
                c.RecommendationId == r.Id &&
                c.TaskId == r.SelectedTaskId &&
                c.OperationType == opType));
        }
        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(r => r.CreatedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(r => r.CreatedAt <= query.ToDate.Value);

        var total = await dbQuery.CountAsync(ct);
        var items = await dbQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new TaskRecommendationListItemDto
            {
                Id = r.Id,
                UserId = r.UserId,
                SourceTaskType = r.SourceTaskType,
                SourceTaskId = r.SourceTaskId,
                Status = r.Status,
                SelectedTaskType = r.SelectedTaskType,
                SelectedTaskId = r.SelectedTaskId,
                SelectedScore = r.SelectedScore,
                ReasonCode = r.ReasonCode,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<TaskRecommendationListItemDto>
        {
            Items = items,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<AcceptTaskRecommendationResponse> AcceptAsync(
        Guid id, AcceptTaskRecommendationRequest request, Guid tenantId, Guid userId, string actor, string traceId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.IdempotencyKey))
        {
            throw new ArgumentException("ACCEPT_IDEMPOTENCY_KEY_REQUIRED");
        }

        Exception? pendingBusinessException = null;
        AcceptTaskRecommendationResponse? earlyResponse = null;
        var committed = false;

        // Chia sẻ 1 connection cho cả TaskInterleaving + Inventory (tránh lỗi transaction không cùng connection)
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var transaction = await connection.BeginTransactionAsync(ct);
        await _context.Database.UseTransactionAsync(transaction, ct);
        _inventoryContext.Database.SetDbConnection(connection);
        await _inventoryContext.Database.UseTransactionAsync(transaction, ct);

        try
        {
            var existingAccepted = await _context.TaskRecommendations
                .Where(r => r.TenantId == tenantId && r.AcceptIdempotencyKey == request.IdempotencyKey && r.Status == "Accepted")
                .FirstOrDefaultAsync(ct);

            if (existingAccepted != null)
            {
                if (existingAccepted.Id == id)
                {
                    earlyResponse = new AcceptTaskRecommendationResponse
                    {
                        RecommendationId = existingAccepted.Id,
                        TaskType = existingAccepted.SelectedTaskType ?? "MobileTask",
                        TaskId = existingAccepted.SelectedTaskId ?? Guid.Empty,
                        Status = existingAccepted.Status,
                        AssignedToUserId = userId.ToString(),
                        AcceptedAt = existingAccepted.AcceptedAt ?? DateTime.UtcNow,
                        TraceId = traceId
                    };
                    await transaction.RollbackAsync(ct);
                    committed = true; // đã thoát TX sạch
                    return earlyResponse;
                }
                throw new InvalidOperationException("IDEMPOTENCY_KEY_CONFLICT");
            }

            var rec = await _context.TaskRecommendations
                .Where(r => r.TenantId == tenantId && r.Id == id)
                .FirstOrDefaultAsync(ct);

            if (rec == null)
            {
                throw new KeyNotFoundException("TASK_RECOMMENDATION_NOT_FOUND");
            }

            if (rec.Status == "Accepted")
            {
                if (rec.AcceptIdempotencyKey == request.IdempotencyKey)
                {
                    earlyResponse = new AcceptTaskRecommendationResponse
                    {
                        RecommendationId = rec.Id,
                        TaskType = rec.SelectedTaskType ?? "MobileTask",
                        TaskId = rec.SelectedTaskId ?? Guid.Empty,
                        Status = rec.Status,
                        AssignedToUserId = userId.ToString(),
                        AcceptedAt = rec.AcceptedAt ?? DateTime.UtcNow,
                        TraceId = traceId
                    };
                    await transaction.RollbackAsync(ct);
                    committed = true;
                    return earlyResponse;
                }
                throw new InvalidOperationException("TASK_RECOMMENDATION_ALREADY_ACCEPTED");
            }

            if (rec.Status != "Open")
            {
                throw new InvalidOperationException("TASK_RECOMMENDATION_STATE_CONFLICT");
            }

            if (rec.ExpiresAt < DateTime.UtcNow)
            {
                rec.Status = "Expired";
                rec.ReasonCode = "TASK_EXPIRED";
                rec.UpdatedAt = DateTime.UtcNow;
                rec.UpdatedBy = actor;
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                committed = true;

                _logger.LogInformation(
                    "event={Event} recommendationId={RecommendationId} tenantId={TenantId} userId={UserId} traceId={TraceId} status={Status}",
                    "task_interleaving.recommendation.expired", rec.Id, tenantId, userId, traceId, rec.Status);

                pendingBusinessException = new InvalidOperationException("TASK_RECOMMENDATION_EXPIRED");
            }
            else if (!rec.SelectedTaskId.HasValue)
            {
                throw new InvalidOperationException("TASK_NOT_ELIGIBLE");
            }
            else
            {
                var task = await _inventoryContext.MobileTasks
                    .Where(t => t.TenantId == tenantId && t.Id == rec.SelectedTaskId.Value)
                    .FirstOrDefaultAsync(ct);

                if (task == null || task.Status != "Open" || task.AssignedUser != null)
                {
                    rec.Status = "Superseded";
                    rec.ReasonCode = "TASK_CONFLICT";
                    rec.UpdatedAt = DateTime.UtcNow;
                    rec.UpdatedBy = actor;
                    await _context.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    committed = true;

                    _logger.LogInformation(
                        "event={Event} recommendationId={RecommendationId} tenantId={TenantId} userId={UserId} traceId={TraceId} status={Status} reasonCode={ReasonCode}",
                        "task_interleaving.recommendation.conflict", rec.Id, tenantId, userId, traceId, rec.Status, rec.ReasonCode);

                    pendingBusinessException = new InvalidOperationException("TASK_ALREADY_ASSIGNED");
                }
                else
                {
                    task.Status = "In_Progress";
                    task.AssignedUser = actor;
                    task.UpdatedAt = DateTime.UtcNow;
                    task.UpdatedBy = actor;

                    rec.Status = "Accepted";
                    rec.AcceptIdempotencyKey = request.IdempotencyKey;
                    rec.AcceptedAt = DateTime.UtcNow;
                    rec.UpdatedAt = DateTime.UtcNow;
                    rec.UpdatedBy = actor;

                    await _inventoryContext.SaveChangesAsync(ct);
                    await _context.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    committed = true;

                    _logger.LogInformation(
                        "event={Event} recommendationId={RecommendationId} tenantId={TenantId} userId={UserId} traceId={TraceId} status={Status} taskId={TaskId}",
                        "task_interleaving.recommendation.accepted", rec.Id, tenantId, userId, traceId, rec.Status, task.Id);

                    return new AcceptTaskRecommendationResponse
                    {
                        RecommendationId = rec.Id,
                        TaskType = rec.SelectedTaskType ?? "MobileTask",
                        TaskId = task.Id,
                        Status = rec.Status,
                        AssignedToUserId = userId.ToString(),
                        AcceptedAt = rec.AcceptedAt.Value,
                        TraceId = traceId
                    };
                }
            }
        }
        catch (Exception)
        {
            if (!committed)
            {
                try { await transaction.RollbackAsync(ct); } catch { /* ignore */ }
            }
            throw;
        }

        if (pendingBusinessException != null)
        {
            throw pendingBusinessException;
        }

        throw new InvalidOperationException("TASK_RECOMMENDATION_STATE_CONFLICT");
    }

    public async Task<RejectTaskRecommendationResponse> RejectAsync(
        Guid id, RejectTaskRecommendationRequest request, Guid tenantId, string actor, string traceId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ReasonCode))
        {
            throw new ArgumentException("REJECT_REASON_REQUIRED");
        }

        if (!TaskInterleavingScorer.IsValidRejectReason(request.ReasonCode))
        {
            throw new ArgumentException("INVALID_REASON_CODE");
        }

        var rec = await _context.TaskRecommendations
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);

        if (rec == null)
        {
            throw new KeyNotFoundException("TASK_RECOMMENDATION_NOT_FOUND");
        }

        if (rec.Status == "Accepted")
        {
            throw new InvalidOperationException("TASK_RECOMMENDATION_ALREADY_ACCEPTED");
        }

        if (rec.Status == "Rejected")
        {
            return new RejectTaskRecommendationResponse
            {
                RecommendationId = rec.Id,
                Status = rec.Status,
                ReasonCode = rec.ReasonCode ?? request.ReasonCode,
                RejectedAt = rec.RejectedAt ?? DateTime.UtcNow,
                TraceId = traceId
            };
        }

        rec.Status = "Rejected";
        rec.ReasonCode = request.ReasonCode;
        rec.DecisionNote = request.Note;
        rec.RejectedAt = DateTime.UtcNow;
        rec.UpdatedAt = DateTime.UtcNow;
        rec.UpdatedBy = actor;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "event={Event} recommendationId={RecommendationId} tenantId={TenantId} actor={Actor} traceId={TraceId} status={Status} reasonCode={ReasonCode}",
            "task_interleaving.recommendation.rejected", rec.Id, tenantId, actor, traceId, rec.Status, rec.ReasonCode);

        return new RejectTaskRecommendationResponse
        {
            RecommendationId = rec.Id,
            Status = rec.Status,
            ReasonCode = rec.ReasonCode,
            RejectedAt = rec.RejectedAt.Value,
            TraceId = traceId
        };
    }

    public async Task<TaskInterleavingKpiResponse> GetKpiAsync(TaskInterleavingKpiQuery query, Guid tenantId, CancellationToken ct)
    {
        var recsQuery = _context.TaskRecommendations.Where(r => r.TenantId == tenantId);

        if (query.UserId.HasValue)
            recsQuery = recsQuery.Where(r => r.UserId == query.UserId.Value);
        if (query.ShiftId.HasValue)
            recsQuery = recsQuery.Where(r => r.ShiftId == query.ShiftId.Value);
        if (query.ZoneId.HasValue)
            recsQuery = recsQuery.Where(r => r.CurrentZoneId == query.ZoneId.Value);
        if (query.FromDate.HasValue)
            recsQuery = recsQuery.Where(r => r.CreatedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            recsQuery = recsQuery.Where(r => r.CreatedAt <= query.ToDate.Value);

        var list = await recsQuery.ToListAsync(ct);
        var total = list.Count;
        if (total == 0) return new TaskInterleavingKpiResponse();

        var accepted = list.Count(r => r.Status == "Accepted");
        var rejected = list.Count(r => r.Status == "Rejected");
        var noCandidate = list.Count(r => r.Status == "NoCandidate");
        var superseded = list.Count(r => r.Status == "Superseded" && r.ReasonCode == "TASK_CONFLICT");

        var candidatesQuery = _context.TaskRecommendationCandidates.Where(c => c.TenantId == tenantId);
        if (query.ZoneId.HasValue)
            candidatesQuery = candidatesQuery.Where(c => c.ZoneId == query.ZoneId.Value);
        var cList = await candidatesQuery.ToListAsync(ct);
        var sameZoneCount = cList.Count(c => c.ZoneId.HasValue && list.Any(r => r.Id == c.RecommendationId && r.CurrentZoneId == c.ZoneId));

        var decisionSeconds = list
            .Where(r => (r.Status == "Accepted" && r.AcceptedAt.HasValue) || (r.Status == "Rejected" && r.RejectedAt.HasValue))
            .Select(r => r.Status == "Accepted"
                ? (r.AcceptedAt!.Value - r.CreatedAt).TotalSeconds
                : (r.RejectedAt!.Value - r.CreatedAt).TotalSeconds)
            .ToList();

        return new TaskInterleavingKpiResponse
        {
            AcceptRate = (decimal)accepted / total,
            RejectRate = (decimal)rejected / total,
            NoCandidateRate = (decimal)noCandidate / total,
            AverageSelectedScore = list.Any(r => r.SelectedScore.HasValue)
                ? list.Where(r => r.SelectedScore.HasValue).Average(r => r.SelectedScore!.Value)
                : 0,
            AverageDecisionSeconds = decisionSeconds.Count > 0 ? (decimal)decisionSeconds.Average() : 0,
            ConflictRate = (decimal)superseded / total,
            SameZoneSuggestionRate = cList.Count > 0 ? (decimal)sameZoneCount / cList.Count : 0
        };
    }
}
