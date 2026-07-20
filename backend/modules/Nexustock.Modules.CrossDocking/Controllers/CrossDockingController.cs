using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.CrossDocking.DTOs;
using Nexustock.Modules.CrossDocking.Services;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inbound.Services;
using Nexustock.Modules.Observability.Services;

namespace Nexustock.Modules.CrossDocking.Controllers;

[Authorize]
[ApiController]
[Route("api/cross-docking")]
public class CrossDockingController : ControllerBase
{
    private readonly ICrossDockingService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IFeatureFlagService _featureFlags;

    public CrossDockingController(
        ICrossDockingService service,
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
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    private string GetActor() => User.Identity?.Name ?? User.FindFirst("sub")?.Value ?? "system";

    private async Task<IActionResult?> CheckFeatureFlagAsync()
    {
        var enabled = await _featureFlags.IsEnabledAsync("FF_CROSS_DOCKING_ENABLED");
        if (!enabled) return StatusCode(403, new { errorCode = "FEATURE_DISABLED", message = "Cross-docking feature is currently disabled." });
        return null;
    }

    [HttpGet("candidates")]
    public async Task<IActionResult> ListCandidates(
        [FromQuery] Guid? lotId,
        [FromQuery] string? status,
        [FromQuery] Guid? itemId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("cross_docking.read")) return Forbid();

        var result = await _service.ListAsync(new ListCandidatesQuery(GetTenantId(), lotId, status, itemId, page, Math.Min(pageSize, 100)), ct);
        return Ok(new ListCandidatesResponse(result.Items, result.Total, result.Page, result.PageSize));
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateRequest request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("cross_docking.create")) return Forbid();

        try
        {
            var result = await _service.EvaluateAsync(request.LotId, GetTenantId(), GetActor(), ct);
            return Ok(result);
        }
        catch (CrossDockingException ex)
        {
            return StatusCode(ex.HttpStatus, new { errorCode = ex.ErrorCode, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("cross_docking.approve")) return Forbid();

        try
        {
            await _service.AcceptAsync(id, GetTenantId(), GetActor(), ct);
            return Ok(new { message = "Candidate accepted." });
        }
        catch (CrossDockingException ex)
        {
            return StatusCode(ex.HttpStatus, new { errorCode = ex.ErrorCode, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("cross_docking.approve")) return Forbid();

        try
        {
            await _service.RejectAsync(id, request.Reason, GetTenantId(), GetActor(), ct);
            return Ok(new { message = "Candidate rejected." });
        }
        catch (CrossDockingException ex)
        {
            return StatusCode(ex.HttpStatus, new { errorCode = ex.ErrorCode, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("cross_docking.read")) return Forbid();

        var detail = await _service.GetAsync(id, GetTenantId(), ct);
        if (detail is null) return NotFound(new { errorCode = "CANDIDATE_NOT_FOUND", message = $"Candidate {id} not found." });
        return Ok(detail);
    }
}
