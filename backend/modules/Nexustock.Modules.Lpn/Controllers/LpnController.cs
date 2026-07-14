using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Lpn.Dtos;
using Nexustock.Modules.Lpn.Services;

namespace Nexustock.Modules.Lpn.Controllers;

[Authorize]
[ApiController]
[Route("api/lpns")]
public class LpnController : ControllerBase
{
    private readonly ILpnService _lpnService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;

    public LpnController(
        ILpnService lpnService,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService)
    {
        _lpnService = lpnService;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpPost]
    public async Task<IActionResult> CreateLpn([FromBody] CreateLpnDto dto)
    {
        if (!await HasPermissionAsync("lpn.create"))
        {
            return Forbid();
        }

        var username = User.Identity?.Name ?? "System";
        try
        {
            var lpn = await _lpnService.CreateLpnAsync(GetTenantId(), dto, username);
            return Ok(lpn);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.ToString() });
        }
    }

    [HttpPost("{id}/attach")]
    public async Task<IActionResult> AttachToLpn(Guid id, [FromBody] AttachLpnDto dto)
    {
        if (!await HasPermissionAsync("lpn.update"))
        {
            return Forbid();
        }

        if (dto.Qty <= 0)
        {
            return BadRequest("Số lượng đóng gói phải lớn hơn 0.");
        }

        var username = User.Identity?.Name ?? "System";
        try
        {
            var success = await _lpnService.AttachToLpnAsync(GetTenantId(), id, dto, username);
            return Ok(new { success, message = "Đã đóng gói hàng hóa vào LPN thành công." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    [HttpPost("{id}/detach")]
    public async Task<IActionResult> DetachFromLpn(Guid id, [FromBody] DetachLpnDto dto)
    {
        if (!await HasPermissionAsync("lpn.update"))
        {
            return Forbid();
        }

        if (dto.Qty <= 0)
        {
            return BadRequest("Số lượng rút phải lớn hơn 0.");
        }

        var username = User.Identity?.Name ?? "System";
        try
        {
            var success = await _lpnService.DetachFromLpnAsync(GetTenantId(), id, dto, username);
            return Ok(new { success, message = "Đã rút hàng hóa khỏi LPN thành công." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    [HttpPost("{id}/move")]
    public async Task<IActionResult> MoveLpn(Guid id, [FromBody] MoveLpnDto dto)
    {
        if (!await HasPermissionAsync("lpn.update") && !await HasPermissionAsync("lpn.execute"))
        {
            return Forbid();
        }

        var username = User.Identity?.Name ?? "System";
        try
        {
            var success = await _lpnService.MoveLpnAsync(GetTenantId(), id, dto, username);
            return Ok(new { success, message = "LPN đã được dịch chuyển thành công." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetLpns()
    {
        if (!await HasPermissionAsync("lpn.read"))
        {
            return Forbid();
        }

        try
        {
            var lpns = await _lpnService.GetLpnsAsync(GetTenantId());
            return Ok(lpns);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/events")]
    public async Task<IActionResult> GetLpnEvents(Guid id)
    {
        if (!await HasPermissionAsync("lpn.read"))
        {
            return Forbid();
        }

        try
        {
            var events = await _lpnService.GetLpnEventsAsync(GetTenantId(), id);
            return Ok(events);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
