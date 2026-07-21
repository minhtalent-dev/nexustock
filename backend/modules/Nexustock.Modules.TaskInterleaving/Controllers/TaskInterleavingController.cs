using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.Observability.Services;
using Nexustock.Modules.TaskInterleaving.Dtos;
using Nexustock.Modules.TaskInterleaving.Services;

namespace Nexustock.Modules.TaskInterleaving.Controllers;

[Authorize]
[ApiController]
[Route("api/task-interleaving")]
public class TaskInterleavingController : ControllerBase
{
    private readonly ITaskInterleavingService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IFeatureFlagService _featureFlags;

    public TaskInterleavingController(
        ITaskInterleavingService service,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IFeatureFlagService featureFlags)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _featureFlags = featureFlags;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("USER_NOT_AUTHENTICATED");
        }
        return userId;
    }

    private string GetActor() => User.Identity?.Name ?? User.FindFirst("sub")?.Value ?? "system";

    private async Task<IActionResult?> CheckFeatureFlagAsync()
    {
        var enabled = await _featureFlags.IsEnabledAsync("FF_TASK_INTERLEAVING_ENABLED");
        if (!enabled)
        {
            return StatusCode(403, new { errorCode = "TASK_INTERLEAVING_DISABLED", message = "Task interleaving feature is currently disabled." });
        }
        return null;
    }

    [HttpGet("next")]
    public async Task<IActionResult> GetNext([FromQuery] NextTaskRecommendationQuery query, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;

        if (!await HasPermissionAsync("task_interleaving.read")) return Forbid();

        try
        {
            var result = await _service.GetNextAsync(query, GetTenantId(), GetUserId(), GetActor(), HttpContext.TraceIdentifier, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(401, new { errorCode = ex.Message, message = "User not authenticated.", traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> List([FromQuery] TaskRecommendationListQuery query, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;

        if (!await HasPermissionAsync("task_interleaving.read")) return Forbid();

        var result = await _service.ListAsync(query, GetTenantId(), ct);
        return Ok(result);
    }

    [HttpGet("recommendations/{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;

        if (!await HasPermissionAsync("task_interleaving.read")) return Forbid();

        try
        {
            var result = await _service.GetDetailAsync(id, GetTenantId(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.Message, message = "Task recommendation log not found.", traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("recommendations/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, [FromBody] AcceptTaskRecommendationRequest request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;

        if (!await HasPermissionAsync("task_interleaving.accept")) return Forbid();

        try
        {
            var result = await _service.AcceptAsync(id, request, GetTenantId(), GetUserId(), GetActor(), HttpContext.TraceIdentifier, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errorCode = ex.Message, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.Message, message = "Task recommendation not found.", traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex) when (ex.Message == "TASK_ALREADY_ASSIGNED")
        {
            return StatusCode(409, new { errorCode = ex.Message, message = "Task has already been assigned to another user.", traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex) when (ex.Message == "TASK_RECOMMENDATION_EXPIRED")
        {
            return StatusCode(409, new { errorCode = ex.Message, message = "Task recommendation has expired.", traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(409, new { errorCode = "TASK_RECOMMENDATION_CONFLICT", message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("recommendations/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectTaskRecommendationRequest request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;

        if (!await HasPermissionAsync("task_interleaving.reject")) return Forbid();

        try
        {
            var result = await _service.RejectAsync(id, request, GetTenantId(), GetActor(), HttpContext.TraceIdentifier, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errorCode = ex.Message, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.Message, message = "Task recommendation not found.", traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(409, new { errorCode = "TASK_RECOMMENDATION_CONFLICT", message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpi([FromQuery] TaskInterleavingKpiQuery query, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;

        if (!await HasPermissionAsync("task_interleaving.read")) return Forbid();

        var result = await _service.GetKpiAsync(query, GetTenantId(), ct);
        return Ok(result);
    }
}
