using System;
using System.Threading;
using System.Threading.Tasks;
using Nexustock.Modules.TaskInterleaving.Dtos;

namespace Nexustock.Modules.TaskInterleaving.Services;

public interface ITaskInterleavingService
{
    Task<NextTaskRecommendationResponse> GetNextAsync(NextTaskRecommendationQuery query, Guid tenantId, Guid userId, string actor, string traceId, CancellationToken ct);
    Task<TaskRecommendationDetailResponse> GetDetailAsync(Guid id, Guid tenantId, CancellationToken ct);
    Task<PagedResult<TaskRecommendationListItemDto>> ListAsync(TaskRecommendationListQuery query, Guid tenantId, CancellationToken ct);
    Task<AcceptTaskRecommendationResponse> AcceptAsync(Guid id, AcceptTaskRecommendationRequest request, Guid tenantId, Guid userId, string actor, string traceId, CancellationToken ct);
    Task<RejectTaskRecommendationResponse> RejectAsync(Guid id, RejectTaskRecommendationRequest request, Guid tenantId, string actor, string traceId, CancellationToken ct);
    Task<TaskInterleavingKpiResponse> GetKpiAsync(TaskInterleavingKpiQuery query, Guid tenantId, CancellationToken ct);
}
