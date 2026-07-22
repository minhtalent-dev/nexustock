using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Allocation.Dtos;
using Nexustock.Modules.Allocation.Services;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Services;

namespace Nexustock.Modules.Allocation.Controllers;

/// <summary>
/// P36: GeneratePicks SoT qua AllocationService (tránh circular Inventory→Allocation).
/// URL giữ /api/outbound/shipments/{id}/generate-picks cho FE.
/// </summary>
[Authorize]
[ApiController]
[Route("api/outbound")]
public class OutboundGeneratePicksController : ControllerBase
{
    private readonly IAllocationService _allocationService;
    private readonly InventoryDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;

    public OutboundGeneratePicksController(
        IAllocationService allocationService,
        InventoryDbContext db,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService)
    {
        _allocationService = allocationService;
        _db = db;
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

    [HttpPost("shipments/{id:guid}/generate-picks")]
    public async Task<IActionResult> GeneratePicks(Guid id, [FromQuery] string strategy = "FEFO")
    {
        if (!await HasPermissionAsync("Outbound.Picks.Execute"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var shipment = await _db.Shipments
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (shipment == null)
        {
            return NotFound(new { errorCode = "SHIPMENT_NOT_FOUND", message = "Không tìm thấy đơn xuất" });
        }

        var existingPicks = await _db.PickTasks.AnyAsync(p =>
            p.ShipmentId == id &&
            p.TenantId == tenantId &&
            p.Status != "Cancelled");
        if (existingPicks)
        {
            return BadRequest(new
            {
                errorCode = "PICKS_ALREADY_EXIST",
                message = "Đã có nhiệm vụ pick cho đơn xuất này"
            });
        }

        if (shipment.Status != "Open")
        {
            return BadRequest(new
            {
                errorCode = "INVALID_SHIPMENT_STATUS",
                message = "Trạng thái đơn xuất không hợp lệ để phân bổ"
            });
        }

        var normalizedStrategy = strategy.Equals("FIFO", StringComparison.OrdinalIgnoreCase)
            ? "FIFO"
            : "FEFO";

        try
        {
            var alloc = await _allocationService.AllocateAsync(tenantId, new ReserveRequestDto
            {
                ShipmentId = id,
                Strategy = normalizedStrategy,
                AllowPartial = false,
                ReservationTtlMinutes = 1440,
                CreatePickTasks = true
            }, username);

            var pickCount = await _db.PickTasks.CountAsync(p =>
                p.ShipmentId == id &&
                p.TenantId == tenantId &&
                p.Status == "Pending");

            return Ok(new
            {
                message = "Sinh pick tasks thành công",
                shipmentId = id,
                status = alloc.Status,
                pickTaskCount = pickCount
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "INSUFFICIENT_INVENTORY", message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { errorCode = "SHIPMENT_NOT_FOUND", message = "Không tìm thấy đơn xuất" });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(409, new { errorCode = "LOCK_TIMEOUT", message = ex.Message });
        }
    }
}
