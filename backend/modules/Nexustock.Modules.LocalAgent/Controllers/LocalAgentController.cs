using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nexustock.Modules.LocalAgent.Services;
using Nexustock.Modules.LocalAgent.DTOs;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.LocalAgent.Controllers;

[ApiController]
[Route("api/agent/stations")]
[Authorize]
public class LocalAgentController : ControllerBase
{
    private readonly ILocalAgentService _agentService;
    private readonly IUserPermissionService _permissionService;
    private readonly ITenantProvider _tenantProvider;

    public LocalAgentController(
        ILocalAgentService agentService, 
        IUserPermissionService permissionService,
        ITenantProvider tenantProvider)
    {
        _agentService = agentService;
        _permissionService = permissionService;
        _tenantProvider = tenantProvider;
    }

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpPost("pairing-code")]
    public async Task<IActionResult> GeneratePairingCode([FromBody] GeneratePairingCodeRequestDto dto)
    {
        if (!await HasPermissionAsync("local_agent.pair"))
        {
            return Forbid();
        }

        var tenantId = _tenantProvider.TenantId;
        var username = User.Identity!.Name!;
        var result = await _agentService.GeneratePairingCodeAsync(tenantId, username, dto);
        return Ok(result);
    }

    [HttpPost("confirm-pair")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPair([FromBody] ConfirmPairRequestDto dto)
    {
        try
        {
            var result = await _agentService.ConfirmPairAsync(dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{stationId}/heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> Heartbeat(Guid stationId, [FromBody] HeartbeatRequestDto dto)
    {
        if (!Request.Headers.TryGetValue("X-Agent-Token", out var tokenValues))
        {
            return Unauthorized(new { code = "agent.unpaired", message = "Missing authentication token." });
        }

        var token = tokenValues.ToString();
        try
        {
            var result = await _agentService.HeartbeatAsync(stationId, token, dto);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { code = "backend.revoked", message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStations([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
    {
        if (!await HasPermissionAsync("local_agent.view"))
        {
            return Forbid();
        }

        var tenantId = _tenantProvider.TenantId;
        var result = await _agentService.GetStationsAsync(tenantId, page, pageSize, search);
        return Ok(result);
    }

    [HttpPost("{stationId}/revoke")]
    public async Task<IActionResult> RevokeStation(Guid stationId, [FromBody] RevokeStationRequestDto dto)
    {
        if (!await HasPermissionAsync("local_agent.revoke"))
        {
            return Forbid();
        }

        var tenantId = _tenantProvider.TenantId;
        try
        {
            await _agentService.RevokeStationAsync(tenantId, stationId, dto);
            return Ok(new { status = "revoked" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
