using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.LaborTracking.DTOs;
using Nexustock.Modules.LaborTracking.Services;
using Nexustock.Modules.MasterData.Services;
using Nexustock.Modules.Observability.Services;

namespace Nexustock.Modules.LaborTracking.Controllers;

[Authorize]
[ApiController]
[Route("api/labor")]
public class LaborController : ControllerBase
{
    private readonly ILaborTrackingService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IFeatureFlagService _featureFlags;

    public LaborController(
        ILaborTrackingService service,
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
        var enabled = await _featureFlags.IsEnabledAsync("FF_LABOR_TRACKING_ENABLED");
        if (!enabled)
        {
            return StatusCode(403, new { errorCode = "FEATURE_DISABLED", message = "Labor tracking feature is currently disabled." });
        }
        return null;
    }

    [HttpPost("sessions/start")]
    public async Task<IActionResult> Start([FromBody] StartLaborSessionRequest request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.create")) return Forbid();

        try
        {
            var result = await _service.StartAsync(request, GetTenantId(), GetActor(), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "LABOR_SESSION_ALREADY_ACTIVE")
        {
            return StatusCode(409, new { errorCode = ex.Message, message = "User already has an active labor session.", traceId = HttpContext.TraceIdentifier });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.Message, message = "Source task not found.", traceId = HttpContext.TraceIdentifier });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errorCode = ex.Message, message = "Invalid source task specifications.", traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("sessions/{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.update")) return Forbid();

        try
        {
            var result = await _service.PauseAsync(id, GetTenantId(), GetActor(), HttpContext.TraceIdentifier, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.Message, message = "Labor session not found.", traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(409, new { errorCode = ex.Message, message = "Labor session is not running.", traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("sessions/{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.update")) return Forbid();

        try
        {
            var result = await _service.ResumeAsync(id, GetTenantId(), GetActor(), HttpContext.TraceIdentifier, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.Message, message = "Labor session not found.", traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(409, new { errorCode = ex.Message, message = "Transition invalid or clock drift detected.", traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("sessions/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.update")) return Forbid();

        try
        {
            var result = await _service.CompleteAsync(id, GetTenantId(), GetActor(), HttpContext.TraceIdentifier, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.Message, message = "Labor session not found.", traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(409, new { errorCode = ex.Message, message = "Invalid session status or negative duration.", traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("sessions/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelLaborSessionRequest request, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.update")) return Forbid();

        try
        {
            var result = await _service.CancelAsync(id, request.Reason, GetTenantId(), GetActor(), HttpContext.TraceIdentifier, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.Message, message = "Labor session not found.", traceId = HttpContext.TraceIdentifier });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errorCode = ex.Message, message = "Cancellation reason is required.", traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(409, new { errorCode = ex.Message, message = "Session cannot be cancelled from current status.", traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions([FromQuery] LaborSessionsQuery query, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.read")) return Forbid();

        var result = await _service.ListAsync(query, GetTenantId(), ct);
        return Ok(result);
    }

    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpi([FromQuery] LaborKpiQuery query, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.read")) return Forbid();

        var result = await _service.GetKpiAsync(query, GetTenantId(), ct);
        return Ok(result);
    }

    [HttpGet("kpi/charts")]
    public async Task<IActionResult> GetKpiCharts([FromQuery] LaborKpiQuery query, CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.read")) return Forbid();

        var result = await _service.GetKpiChartsAsync(query, GetTenantId(), ct);
        return Ok(result);
    }

    [HttpGet("shifts/current")]
    public async Task<IActionResult> GetCurrentShift(CancellationToken ct = default)
    {
        var flagCheck = await CheckFeatureFlagAsync();
        if (flagCheck is not null) return flagCheck;
        if (!await HasPermissionAsync("labor_tracking.read")) return Forbid();

        var result = await _service.GetCurrentShiftAsync(GetActor(), GetTenantId(), ct);
        return Ok(result);
    }
}
