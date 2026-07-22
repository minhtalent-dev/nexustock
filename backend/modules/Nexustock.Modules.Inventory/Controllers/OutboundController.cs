using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Dtos;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Qc.Abstractions;

namespace Nexustock.Modules.Inventory.Controllers;

[Authorize]
[ApiController]
[Route("api/outbound")]
public class OutboundController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly MasterDataDbContext _masterContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IWeightValidationService _weightValidationService;
    private readonly IQcGateService _qcGate;

    public OutboundController(
        InventoryDbContext context,
        MasterDataDbContext masterContext,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IWeightValidationService weightValidationService,
        IQcGateService qcGate)
    {
        _context = context;
        _masterContext = masterContext;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _weightValidationService = weightValidationService;
        _qcGate = qcGate;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpGet("shipments")]
    public async Task<IActionResult> GetShipments()
    {
        if (!await HasPermissionAsync("Outbound.Shipments.View"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var shipments = await _context.Shipments
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var partnerIds = shipments.Select(s => s.PartnerId).Distinct().ToList();
        var partners = await _masterContext.Partners
            .Where(p => partnerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var response = shipments.Select(s => new ShipmentListResponseDto
        {
            Id = s.Id,
            ShipmentNo = s.ShipmentNo,
            PartnerId = s.PartnerId,
            PartnerName = partners.TryGetValue(s.PartnerId, out var name) ? name : "Unknown Partner",
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy
        }).ToList();

        return Ok(response);
    }

    [HttpGet("shipments/{id:guid}")]
    public async Task<IActionResult> GetShipmentDetails(Guid id)
    {
        if (!await HasPermissionAsync("Outbound.Shipments.View"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var shipment = await _context.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (shipment == null) return NotFound("Không tìm thấy đơn xuất");

        var items = await _context.ShipmentItems
            .Where(i => i.ShipmentId == id && i.TenantId == tenantId)
            .ToListAsync();

        var itemIds = items.Select(i => i.ItemId).Distinct().ToList();
        var products = await _masterContext.Products
            .Where(p => itemIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Code });

        var uomIds = items.Select(i => i.UomId).Distinct().ToList();
        var uoms = await _masterContext.Uoms
            .Where(u => uomIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var itemDtos = items.Select(i => new {
            i.Id,
            i.ItemId,
            ItemName = products.TryGetValue(i.ItemId, out var p) ? p.Name : "Unknown Product",
            ItemCode = products.TryGetValue(i.ItemId, out var p2) ? p2.Code : "Unknown Code",
            UomName = uoms.TryGetValue(i.UomId, out var u) ? u : "Unknown Uom",
            i.RequestedQty,
            i.AllocatedQty,
            i.PickedQty,
            i.PackedQty
        }).ToList();

        var pickTasks = await _context.PickTasks
            .Where(p => p.ShipmentId == id && p.TenantId == tenantId)
            .ToListAsync();

        var locationIds = pickTasks.Select(p => p.FromLocationId).Distinct().ToList();
        var locations = await _masterContext.StorageLocations
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code);

        var pickDtos = pickTasks.Select(p => new {
            p.Id,
            p.ItemId,
            ItemName = products.TryGetValue(p.ItemId, out var prod) ? prod.Name : "Unknown Product",
            p.LotNo,
            p.FromLocationId,
            LocationCode = locations.TryGetValue(p.FromLocationId, out var loc) ? loc : "Unknown Location",
            p.Qty,
            p.PickedQty,
            p.Status,
            p.CreatedAt,
            p.CreatedBy
        }).ToList();

        var packingRecords = await _context.PackingRecords
            .Where(p => p.ShipmentId == id && p.TenantId == tenantId)
            .Select(p => new
            {
                p.Id,
                p.PackageNo,
                p.Weight,
                p.Status,
                p.CreatedAt,
                p.CreatedBy
            })
            .ToListAsync();

        var partner = await _masterContext.Partners.FirstOrDefaultAsync(p => p.Id == shipment.PartnerId);

        return Ok(new {
            shipment = new {
                shipment.Id,
                shipment.ShipmentNo,
                shipment.PartnerId,
                PartnerName = partner?.Name ?? "Unknown Partner",
                shipment.Status,
                shipment.CreatedAt,
                shipment.CreatedBy
            },
            items = itemDtos,
            picks = pickDtos,
            packings = packingRecords
        });
    }

    [HttpPost("shipments")]
    public async Task<IActionResult> CreateShipment([FromBody] CreateShipmentRequestDto dto)
    {
        if (!await HasPermissionAsync("Outbound.Shipments.Create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        // Validate Partner exists in MasterData
        var partnerExists = await _masterContext.Partners.AnyAsync(p => p.Id == dto.PartnerId && p.TenantId == tenantId);
        if (!partnerExists)
        {
            return BadRequest(new { errorCode = "PARTNER_NOT_FOUND", message = "Đối tác không hợp lệ" });
        }

        // Validate Products exist in MasterData
        var itemIds = dto.Items.Select(i => i.ItemId).Distinct().ToList();
        var validItemsCount = await _masterContext.Products.CountAsync(p => itemIds.Contains(p.Id) && p.TenantId == tenantId);
        if (validItemsCount != itemIds.Count)
        {
            return BadRequest(new { errorCode = "INVALID_ITEM", message = "Một hoặc nhiều vật tư không hợp lệ" });
        }

        // Verify unique shipment number
        var dupShipment = await _context.Shipments.AnyAsync(s => s.ShipmentNo == dto.ShipmentNo && s.TenantId == tenantId);
        if (dupShipment)
        {
            return BadRequest(new { errorCode = "DUPLICATE_SHIPMENT_NO", message = "Mã đơn xuất đã tồn tại" });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var shipment = new Shipment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ShipmentNo = dto.ShipmentNo,
                PartnerId = dto.PartnerId,
                Status = "Open",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.Shipments.Add(shipment);

            foreach (var itemDto in dto.Items)
            {
                var item = new ShipmentItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ShipmentId = shipment.Id,
                    ItemId = itemDto.ItemId,
                    UomId = itemDto.UomId,
                    RequestedQty = itemDto.RequestedQty,
                    PickedQty = 0,
                    PackedQty = 0
                };
                _context.ShipmentItems.Add(item);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { id = shipment.Id, message = "Tạo đơn xuất thành công" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("shipments/{id:guid}/generate-picks")]
    public async Task<IActionResult> GeneratePicks(Guid id)
    {
        if (!await HasPermissionAsync("Outbound.Picks.Execute"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var shipment = await _context.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (shipment == null)
        {
            return NotFound(new { errorCode = "SHIPMENT_NOT_FOUND", message = "Không tìm thấy đơn xuất" });
        }

        if (shipment.Status != "Open")
        {
            return BadRequest(new { errorCode = "INVALID_SHIPMENT_STATUS", message = "Trạng thái đơn xuất không hợp lệ để phân bổ" });
        }

        var items = await _context.ShipmentItems.Where(i => i.ShipmentId == id && i.TenantId == tenantId).ToListAsync();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var item in items)
            {
                var remainingToAllocate = item.RequestedQty;

                // FIFO Allocation: Query inventories ordered by LotNo. Filter by QcStatus = Release, lock status
                var inventories = await _context.Inventories
                    .Where(i => i.ItemId == item.ItemId && i.TenantId == tenantId && (i.QtyOnHand - i.QtyReserved) > 0)
                    .OrderBy(i => i.LotNo)
                    .ToListAsync();

                var lotNos = inventories.Select(i => i.LotNo).Distinct().ToList();
                var releasedLots = await _context.Lots
                    .Where(l => l.TenantId == tenantId && l.ItemId == item.ItemId && lotNos.Contains(l.LotNo) && l.QcStatus == "Release")
                    .Select(l => l.LotNo)
                    .ToListAsync();

                var locationIds = inventories.Select(i => i.LocationId).Distinct().ToList();
                var lockedOutboundLocations = await _context.LocationLocks
                    .Where(l => l.TenantId == tenantId && locationIds.Contains(l.LocationId) && (l.LockType == "OUTBOUND" || l.LockType == "ALL"))
                    .Select(l => l.LocationId)
                    .ToListAsync();

                var filteredInventories = inventories
                    .Where(i => releasedLots.Contains(i.LotNo) && !lockedOutboundLocations.Contains(i.LocationId))
                    .ToList();

                var totalAvailable = filteredInventories.Sum(i => i.QtyOnHand - i.QtyReserved);
                if (totalAvailable < remainingToAllocate)
                {
                    return BadRequest(new { errorCode = "INSUFFICIENT_INVENTORY", message = "Không đủ tồn kho khả dụng để phân bổ sản phẩm" });
                }

                foreach (var inv in filteredInventories)
                {
                    if (remainingToAllocate <= 0) break;

                    var qtyAvailable = inv.QtyOnHand - inv.QtyReserved;
                    var allocQty = Math.Min(qtyAvailable, remainingToAllocate);

                    inv.QtyReserved += allocQty;
                    inv.UpdatedAt = DateTime.UtcNow;
                    inv.UpdatedBy = username;

                    var pickTask = new PickTask
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ShipmentId = shipment.Id,
                        ItemId = item.ItemId,
                        LotNo = inv.LotNo,
                        FromLocationId = inv.LocationId,
                        Qty = allocQty,
                        PickedQty = 0,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username
                    };
                    _context.PickTasks.Add(pickTask);

                    remainingToAllocate -= allocQty;
                }
            }

            shipment.Status = "Allocated";
            shipment.UpdatedAt = DateTime.UtcNow;
            shipment.UpdatedBy = username;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Sinh pick tasks thành công" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("picks/{id:guid}/complete")]
    public async Task<IActionResult> CompletePick(Guid id, [FromBody] CompletePickRequestDto dto)
    {
        if (!await HasPermissionAsync("Outbound.Picks.Execute"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";
        var traceId = HttpContext.TraceIdentifier;

        var pickTask = await _context.PickTasks.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (pickTask == null)
        {
            return NotFound(new { errorCode = "PICK_TASK_NOT_FOUND", message = "Không tìm thấy nhiệm vụ pick" });
        }

        if (pickTask.Status != "Pending")
        {
            return BadRequest(new { errorCode = "INVALID_PICK_STATUS", message = "Nhiệm vụ pick đã được xử lý" });
        }

        if (dto.PickedQty <= 0 || dto.PickedQty > pickTask.Qty)
        {
            return BadRequest(new { errorCode = "PICK_QTY_EXCEEDED", message = "Số lượng pick không hợp lệ" });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // QC Gate — SoT Inbound.Lots
            try
            {
                await _qcGate.EnsureLotUsableByLotNoAsync(tenantId, pickTask.ItemId, pickTask.LotNo);
            }
            catch (QcGateException ex)
            {
                return StatusCode(ex.HttpStatus, new { errorCode = ex.ErrorCode, message = ex.Message, traceId = HttpContext.TraceIdentifier });
            }

            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == pickTask.ItemId && i.LotNo == pickTask.LotNo && i.LocationId == pickTask.FromLocationId);

            if (inventory == null || inventory.QtyOnHand < dto.PickedQty)
            {
                return BadRequest(new { errorCode = "INSUFFICIENT_QTY", message = "Tồn kho thực tế không đủ để hoàn thành pick" });
            }

            // Deduct inventory
            inventory.QtyOnHand -= dto.PickedQty;
            inventory.QtyReserved -= dto.PickedQty;
            inventory.UpdatedAt = DateTime.UtcNow;
            inventory.UpdatedBy = username;
            inventory.RowVersion += 1;

            if (inventory.QtyOnHand == 0 && inventory.QtyReserved == 0)
            {
                _context.Inventories.Remove(inventory);
            }

            // Record transaction
            var invTrans = new Entities.InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = pickTask.ItemId,
                LotNo = pickTask.LotNo,
                LocationId = pickTask.FromLocationId,
                TransactionType = "PICK_OUT",
                Qty = -dto.PickedQty,
                TraceId = traceId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.InventoryTransactions.Add(invTrans);

            // Update Pick Task
            pickTask.PickedQty = dto.PickedQty;
            pickTask.Status = "Completed";
            pickTask.UpdatedAt = DateTime.UtcNow;
            pickTask.UpdatedBy = username;

            // Update Shipment Item
            var shipmentItem = await _context.ShipmentItems
                .FirstOrDefaultAsync(si => si.ShipmentId == pickTask.ShipmentId && si.ItemId == pickTask.ItemId && si.TenantId == tenantId);
            if (shipmentItem != null)
            {
                shipmentItem.PickedQty += dto.PickedQty;
            }

            await _context.SaveChangesAsync();

            // Update Shipment Status
            var shipment = await _context.Shipments.FirstAsync(s => s.Id == pickTask.ShipmentId && s.TenantId == tenantId);
            var allPicks = await _context.PickTasks.Where(p => p.ShipmentId == shipment.Id && p.TenantId == tenantId).ToListAsync();
            
            if (allPicks.All(p => p.Status == "Completed"))
            {
                shipment.Status = "Picking"; // Completed picking
            }
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Hoàn thành nhiệm vụ pick" });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return StatusCode(409, new { errorCode = "CONCURRENCY_CONFLICT", message = "Dữ liệu tồn kho đã thay đổi bởi phiên làm việc khác, vui lòng tải lại" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("packing/weight/manual")]
    public async Task<IActionResult> CreateManualWeightOverride([FromBody] ManualWeightOverrideRequestDto dto)
    {
        if (!await HasPermissionAsync("Outbound.Packing.Execute"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { errorCode = "USER_INVALID", message = "Không xác định được người dùng hiện tại" });
        }

        var packageNo = dto.PackageNo.Trim();
        var reason = dto.Reason.Trim();
        if (string.IsNullOrWhiteSpace(packageNo) || string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(new { errorCode = "MANUAL_OVERRIDE_INVALID", message = "Số kiện và lý do nhập tay là bắt buộc" });
        }

        var shipment = await _context.Shipments.FirstOrDefaultAsync(s => s.Id == dto.ShipmentId && s.TenantId == tenantId);
        if (shipment == null)
        {
            return NotFound(new { errorCode = "SHIPMENT_NOT_FOUND", message = "Không tìm thấy đơn xuất" });
        }

        if (shipment.Status != "Picking" && shipment.Status != "Allocated")
        {
            return BadRequest(new { errorCode = "INVALID_SHIPMENT_STATUS", message = "Trạng thái đơn xuất không hợp lệ để nhập cân nặng thủ công" });
        }

        var duplicatePackage = await _context.PackingRecords.AnyAsync(p => p.TenantId == tenantId && p.PackageNo == packageNo);
        if (duplicatePackage)
        {
            return BadRequest(new { errorCode = "DUPLICATE_PACKAGE_NO", message = "Số kiện đã tồn tại" });
        }

        var manualOverride = new ManualWeightOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ShipmentId = shipment.Id,
            PackageNo = packageNo,
            ManualWeight = dto.ManualWeight,
            Reason = reason,
            ApprovedBy = userId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };
        _context.ManualWeightOverrides.Add(manualOverride);
        await _context.SaveChangesAsync();

        return Ok(new ManualWeightOverrideResponseDto
        {
            ManualOverrideId = manualOverride.Id,
            ManualWeight = manualOverride.ManualWeight
        });
    }

    [HttpPost("packing/{shipmentId:guid}/complete")]
    public async Task<IActionResult> CompletePacking(Guid shipmentId, [FromBody] CompletePackingRequestDto dto)
    {
        if (!await HasPermissionAsync("Outbound.Packing.Execute"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { errorCode = "USER_INVALID", message = "Không xác định được người dùng hiện tại" });
        }

        var shipment = await _context.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.TenantId == tenantId);
        if (shipment == null)
        {
            return NotFound(new { errorCode = "SHIPMENT_NOT_FOUND", message = "Không tìm thấy đơn xuất" });
        }

        if (shipment.Status != "Picking" && shipment.Status != "Allocated")
        {
            return BadRequest(new { errorCode = "INVALID_SHIPMENT_STATUS", message = "Trạng thái đơn xuất không hợp lệ để đóng gói" });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var validation = await _weightValidationService.ValidateAsync(dto, shipment, tenantId, userId, HttpContext.RequestAborted);
            if (!validation.Success)
            {
                return BadRequest(new { errorCode = validation.ErrorCode, message = validation.Message });
            }

            var packingRecord = new PackingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ShipmentId = shipmentId,
                PackageNo = dto.PackageNo,
                Weight = validation.Weight,
                WeightSource = validation.WeightSource,
                ScaleStable = validation.ScaleStable,
                ManualOverrideId = validation.ManualOverrideId,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.PackingRecords.Add(packingRecord);

            var items = await _context.ShipmentItems.Where(i => i.ShipmentId == shipmentId && i.TenantId == tenantId).ToListAsync();
            foreach (var item in items)
            {
                item.PackedQty = item.PickedQty; // Basic pack matches pick
            }

            shipment.Status = "Packed";
            shipment.UpdatedAt = DateTime.UtcNow;
            shipment.UpdatedBy = username;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Hoàn tất đóng gói đơn xuất" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
