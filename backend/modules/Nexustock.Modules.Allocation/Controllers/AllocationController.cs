using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.Allocation.Dtos;
using Nexustock.Modules.Allocation.Services;

namespace Nexustock.Modules.Allocation.Controllers;

[Authorize]
[ApiController]
[Route("api/allocation")]
public class AllocationController : ControllerBase
{
    private readonly IAllocationService _allocationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;

    public AllocationController(
        IAllocationService allocationService,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService)
    {
        _allocationService = allocationService;
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

    [HttpPost("reserve")]
    public async Task<IActionResult> Reserve([FromBody] ReserveRequestDto dto)
    {
        if (!await HasPermissionAsync("allocation_reservation.create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        try
        {
            var result = await _allocationService.AllocateAsync(tenantId, dto, username);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = "SHIPMENT_NOT_FOUND", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "INSUFFICIENT_QTY", message = ex.Message });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(409, new { errorCode = "LOCK_TIMEOUT", message = ex.Message });
        }
    }

    [HttpPost("release")]
    public async Task<IActionResult> Release([FromBody] ReleaseRequestDto dto)
    {
        if (!await HasPermissionAsync("allocation_reservation.create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        try
        {
            var success = await _allocationService.ReleaseAsync(tenantId, dto.ShipmentId, username);
            if (success)
            {
                return Ok(new { success = true, message = "Giải phóng giữ hàng thành công." });
            }
            return BadRequest(new { success = false, message = "Không thể giải phóng giữ hàng." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = "SHIPMENT_NOT_FOUND", message = ex.Message });
        }
    }

    [HttpPost("reallocate")]
    public async Task<IActionResult> Reallocate([FromBody] ReleaseRequestDto dto)
    {
        if (!await HasPermissionAsync("allocation_reservation.create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        try
        {
            var result = await _allocationService.ReallocateAsync(tenantId, dto.ShipmentId, username);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = "SHIPMENT_NOT_FOUND", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "INSUFFICIENT_QTY", message = ex.Message });
        }
    }

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability([FromQuery] Guid itemId)
    {
        if (!await HasPermissionAsync("allocation_reservation.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var result = await _allocationService.GetAvailabilityAsync(tenantId, itemId);
        return Ok(result);
    }
}
