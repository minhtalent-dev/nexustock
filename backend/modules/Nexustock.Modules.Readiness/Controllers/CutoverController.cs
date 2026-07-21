using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.Observability.Services;
using Nexustock.Modules.Readiness.Dtos;
using Nexustock.Modules.Readiness.Services;

namespace Nexustock.Modules.Readiness.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/cutover")]
public class CutoverController : ControllerBase
{
    private readonly IReadinessService _service;
    private readonly ICutoverFreezeService _freeze;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IFeatureFlagService _featureFlags;

    public CutoverController(
        IReadinessService service,
        ICutoverFreezeService freeze,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IFeatureFlagService featureFlags)
    {
        _service = service;
        _freeze = freeze;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _featureFlags = featureFlags;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;
    private string GetActor() => User.Identity?.Name ?? User.FindFirst("sub")?.Value ?? "system";
    private string TraceId => HttpContext.TraceIdentifier;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    private async Task<IActionResult?> CheckGateAsync()
    {
        var enabled = await _featureFlags.IsEnabledAsync("FF_READINESS_GATE_ENABLED");
        if (!enabled)
        {
            return StatusCode(403, new { errorCode = "READINESS_DISABLED", message = "Readiness gate feature is currently disabled.", traceId = TraceId });
        }
        return null;
    }

    [HttpGet("logs")]
    public async Task<IActionResult> ListLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var gate = await CheckGateAsync();
        if (gate is not null) return gate;
        if (!await HasPermissionAsync("readiness.read"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.read", traceId = TraceId });

        var result = await _service.ListCutoverLogsAsync(GetTenantId(), page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("freeze-status")]
    public async Task<IActionResult> FreezeStatus(CancellationToken ct = default)
    {
        var gate = await CheckGateAsync();
        if (gate is not null) return gate;
        if (!await HasPermissionAsync("readiness.read"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.read", traceId = TraceId });

        var result = await _freeze.GetStatusAsync(GetTenantId(), ct);
        return Ok(result);
    }

    [HttpPost("freeze")]
    public async Task<IActionResult> Freeze([FromBody] FreezeRequest? request, CancellationToken ct = default)
    {
        var gate = await CheckGateAsync();
        if (gate is not null) return gate;

        var freezeEnabled = await _featureFlags.IsEnabledAsync("FF_CUTOVER_FREEZE_ENABLED");
        if (!freezeEnabled)
            return StatusCode(403, new { errorCode = "CUTOVER_FREEZE_DENIED", message = "Cutover freeze feature flag is disabled.", traceId = TraceId });

        if (!await HasPermissionAsync("readiness.cutover.freeze"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.cutover.freeze", traceId = TraceId });

        var result = await _freeze.FreezeAsync(GetTenantId(), GetActor(), request?.Reason, TraceId, ct);
        return Ok(result);
    }

    [HttpPost("unfreeze")]
    public async Task<IActionResult> Unfreeze([FromBody] FreezeRequest? request, CancellationToken ct = default)
    {
        var gate = await CheckGateAsync();
        if (gate is not null) return gate;

        var freezeEnabled = await _featureFlags.IsEnabledAsync("FF_CUTOVER_FREEZE_ENABLED");
        if (!freezeEnabled)
            return StatusCode(403, new { errorCode = "CUTOVER_FREEZE_DENIED", message = "Cutover freeze feature flag is disabled.", traceId = TraceId });

        if (!await HasPermissionAsync("readiness.cutover.freeze"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.cutover.freeze", traceId = TraceId });

        var result = await _freeze.UnfreezeAsync(GetTenantId(), GetActor(), request?.Reason, TraceId, ct);
        return Ok(result);
    }
}
