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
[Route("api/admin/readiness")]
public class ReadinessController : ControllerBase
{
    private readonly IReadinessProbeService _probe;
    private readonly IReadinessService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IFeatureFlagService _featureFlags;

    public ReadinessController(
        IReadinessProbeService probe,
        IReadinessService service,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IFeatureFlagService featureFlags)
    {
        _probe = probe;
        _service = service;
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

    private async Task<IActionResult?> CheckFeatureFlagAsync()
    {
        var enabled = await _featureFlags.IsEnabledAsync("FF_READINESS_GATE_ENABLED");
        if (!enabled)
        {
            return StatusCode(403, new { errorCode = "READINESS_DISABLED", message = "Readiness gate feature is currently disabled.", traceId = TraceId });
        }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetProbe(CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("readiness.read"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.read", traceId = TraceId });

        var result = await _probe.ProbeAsync(TraceId, ct);
        return Ok(result);
    }

    [HttpGet("uat-runs")]
    public async Task<IActionResult> ListUatRuns([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("readiness.read"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.read", traceId = TraceId });

        var result = await _service.ListUatRunsAsync(GetTenantId(), page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost("uat-runs")]
    public async Task<IActionResult> CreateUatRun([FromBody] CreateUatRunRequest request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("readiness.uat.write"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.uat.write", traceId = TraceId });

        try
        {
            var result = await _service.CreateUatRunAsync(GetTenantId(), request, GetActor(), TraceId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = ex.Message, message = ex.Message, traceId = TraceId });
        }
    }

    [HttpPost("uat-runs/{id:guid}/signoff")]
    public async Task<IActionResult> Signoff(Guid id, [FromBody] SignoffUatRunRequest? request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("readiness.uat.signoff"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.uat.signoff", traceId = TraceId });

        try
        {
            var result = await _service.SignoffUatRunAsync(GetTenantId(), id, request ?? new SignoffUatRunRequest(null, null), GetActor(), TraceId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "UAT_NOT_FOUND")
        {
            return NotFound(new { errorCode = ex.Message, message = "UAT run not found.", traceId = TraceId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = ex.Message, message = "UAT must be Passed before signoff.", traceId = TraceId });
        }
    }

    [HttpPost("incident-drills")]
    public async Task<IActionResult> CreateDrill([FromBody] CreateIncidentDrillRequest request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("readiness.drill.write"))
            return StatusCode(403, new { errorCode = "READINESS_UNAUTHORIZED", message = "Missing readiness.drill.write", traceId = TraceId });

        try
        {
            var result = await _service.CreateIncidentDrillAsync(GetTenantId(), request, GetActor(), TraceId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = ex.Message, message = ex.Message, traceId = TraceId });
        }
    }
}
