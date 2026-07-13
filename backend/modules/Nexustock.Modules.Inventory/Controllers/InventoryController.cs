using System;
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

namespace Nexustock.Modules.Inventory.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly MasterDataDbContext _masterContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;

    public InventoryController(
        InventoryDbContext context,
        MasterDataDbContext masterContext,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService)
    {
        _context = context;
        _masterContext = masterContext;
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

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] Guid? itemId,
        [FromQuery] Guid? locationId,
        [FromQuery] string? lotNo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (!await HasPermissionAsync("Inventory.Balances.View"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var query = _context.Inventories.Where(i => i.TenantId == tenantId);

        if (itemId.HasValue) query = query.Where(i => i.ItemId == itemId.Value);
        if (locationId.HasValue) query = query.Where(i => i.LocationId == locationId.Value);
        if (!string.IsNullOrWhiteSpace(lotNo)) query = query.Where(i => i.LotNo == lotNo);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(i => i.LotNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Join MasterData in memory
        var prodIds = items.Select(i => i.ItemId).Distinct().ToList();
        var locIds = items.Select(i => i.LocationId).Distinct().ToList();

        var products = await _masterContext.Products
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Code });

        var locations = await _masterContext.StorageLocations
            .Where(l => locIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code);

        var response = items.Select(i => new InventoryBalanceResponseDto
        {
            Id = i.Id,
            ItemId = i.ItemId,
            ItemName = products.TryGetValue(i.ItemId, out var p) ? p.Name : "Unknown Item",
            ItemCode = products.TryGetValue(i.ItemId, out var p2) ? p2.Code : "Unknown Code",
            LotNo = i.LotNo,
            LocationId = i.LocationId,
            LocationCode = locations.TryGetValue(i.LocationId, out var lCode) ? lCode : "Unknown Location",
            QtyOnHand = i.QtyOnHand,
            QtyReserved = i.QtyReserved,
            QtyAvailable = i.QtyOnHand - i.QtyReserved
        }).ToList();

        return Ok(new { items = response, totalCount });
    }

    [HttpPost("move")]
    public async Task<IActionResult> MoveInventory([FromBody] MoveInventoryRequestDto dto)
    {
        if (!await HasPermissionAsync("Inventory.Movements.Create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";
        var traceId = HttpContext.TraceIdentifier;

        // 1. Verify QC Status of Lot (LOT_ON_HOLD)
        var lot = await _context.Lots
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.LotNo == dto.LotNo && l.ItemId == dto.ItemId);

        if (lot == null || lot.QcStatus != "Release")
        {
            return BadRequest(new { errorCode = "LOT_ON_HOLD", message = "Lô hàng đang bị giữ kiểm định chất lượng, không được di chuyển" });
        }

        // 2. Verify Source & Destination Location exist
        var sourceLoc = await _masterContext.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.FromLocationId && l.TenantId == tenantId);
        var targetLoc = await _masterContext.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.ToLocationId && l.TenantId == tenantId);
        if (sourceLoc == null || targetLoc == null)
        {
            return BadRequest(new { errorCode = "INVALID_LOCATION", message = "Vị trí nguồn hoặc vị trí đích không hợp lệ" });
        }

        // 3. Verify Source is not locked Outbound (LOCATION_LOCKED)
        var sourceLock = await _context.LocationLocks
            .FirstOrDefaultAsync(l => l.LocationId == dto.FromLocationId && l.TenantId == tenantId);
        if (sourceLock != null && (sourceLock.LockType == "OUTBOUND" || sourceLock.LockType == "ALL"))
        {
            return BadRequest(new { errorCode = "LOCATION_LOCKED", message = "Vị trí nguồn đang bị khóa" });
        }

        // 4. Verify Destination is not locked Inbound (LOCATION_LOCKED)
        var targetLock = await _context.LocationLocks
            .FirstOrDefaultAsync(l => l.LocationId == dto.ToLocationId && l.TenantId == tenantId);
        if (targetLock != null && (targetLock.LockType == "INBOUND" || targetLock.LockType == "ALL"))
        {
            return BadRequest(new { errorCode = "LOCATION_LOCKED", message = "Vị trí đích đang bị khóa" });
        }

        // 5. Verify Source has enough QtyAvailable (INSUFFICIENT_QTY)
        var sourceInv = await _context.Inventories
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == dto.ItemId && i.LotNo == dto.LotNo && i.LocationId == dto.FromLocationId);

        if (sourceInv == null || (sourceInv.QtyOnHand - sourceInv.QtyReserved) < dto.Qty)
        {
            return BadRequest(new { errorCode = "INSUFFICIENT_QTY", message = "Số lượng dịch chuyển vượt quá tồn khả dụng" });
        }

        // 6. Check Capacity Guard (LOCATION_OVER_CAPACITY)
        var currentQtyAtTarget = await _context.Inventories
            .Where(i => i.LocationId == dto.ToLocationId && i.TenantId == tenantId)
            .SumAsync(i => i.QtyOnHand);

        if (currentQtyAtTarget + dto.Qty > targetLoc.MaxCapacity)
        {
            return BadRequest(new { errorCode = "LOCATION_OVER_CAPACITY", message = "Số lượng vượt quá sức chứa tối đa của vị trí đích" });
        }

        // 7. Execute transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Deduct from source
            sourceInv.QtyOnHand -= dto.Qty;
            sourceInv.UpdatedAt = DateTime.UtcNow;
            sourceInv.UpdatedBy = username;
            sourceInv.RowVersion += 1;

            if (sourceInv.QtyOnHand == 0 && sourceInv.QtyReserved == 0)
            {
                _context.Inventories.Remove(sourceInv);
            }

            // Add to destination
            var targetInv = await _context.Inventories
                .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == dto.ItemId && i.LotNo == dto.LotNo && i.LocationId == dto.ToLocationId);

            if (targetInv != null)
            {
                targetInv.QtyOnHand += dto.Qty;
                targetInv.UpdatedAt = DateTime.UtcNow;
                targetInv.UpdatedBy = username;
                targetInv.RowVersion += 1;
            }
            else
            {
                targetInv = new Entities.Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = dto.ItemId,
                    LotNo = dto.LotNo,
                    LocationId = dto.ToLocationId,
                    QtyOnHand = dto.Qty,
                    QtyReserved = 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username,
                    RowVersion = 1
                };
                _context.Inventories.Add(targetInv);
            }

            // Record Movement
            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = dto.ItemId,
                LotNo = dto.LotNo,
                FromLocationId = dto.FromLocationId,
                ToLocationId = dto.ToLocationId,
                Qty = dto.Qty,
                Status = "Completed",
                ReasonCode = dto.ReasonCode,
                TraceId = traceId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.InventoryMovements.Add(movement);

            // Record Ledger transactions
            var transOut = new Entities.InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = dto.ItemId,
                LotNo = dto.LotNo,
                LocationId = dto.FromLocationId,
                TransactionType = "MOVE_OUT",
                Qty = -dto.Qty,
                TraceId = traceId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.InventoryTransactions.Add(transOut);

            var transIn = new Entities.InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = dto.ItemId,
                LotNo = dto.LotNo,
                LocationId = dto.ToLocationId,
                TransactionType = "MOVE_IN",
                Qty = dto.Qty,
                TraceId = traceId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.InventoryTransactions.Add(transIn);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Inventory moved successfully" });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return StatusCode(409, new { errorCode = "CONCURRENCY_CONFLICT", message = "Dữ liệu tồn kho đã thay đổi bởi phiên làm việc khác, vui lòng tải lại trang" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("locations/{id:guid}/lock")]
    public async Task<IActionResult> LockLocation(Guid id, [FromBody] LockLocationRequestDto dto)
    {
        if (!await HasPermissionAsync("Inventory.Locks.Manage"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var location = await _masterContext.StorageLocations.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);
        if (location == null) return NotFound("Vị trí không tồn tại");

        var lockObj = await _context.LocationLocks.FirstOrDefaultAsync(l => l.LocationId == id && l.TenantId == tenantId);
        if (lockObj != null)
        {
            lockObj.LockType = dto.LockType;
            lockObj.ReasonCode = dto.ReasonCode;
            lockObj.LockedBy = username;
            lockObj.LockedAt = DateTime.UtcNow;
        }
        else
        {
            lockObj = new LocationLock
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LocationId = id,
                LockType = dto.LockType,
                ReasonCode = dto.ReasonCode,
                LockedBy = username,
                LockedAt = DateTime.UtcNow
            };
            _context.LocationLocks.Add(lockObj);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Vị trí kệ đã được khóa" });
    }

    [HttpPost("locations/{id:guid}/unlock")]
    public async Task<IActionResult> UnlockLocation(Guid id)
    {
        if (!await HasPermissionAsync("Inventory.Locks.Manage"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();

        var lockObj = await _context.LocationLocks.FirstOrDefaultAsync(l => l.LocationId == id && l.TenantId == tenantId);
        if (lockObj == null) return BadRequest("Vị trí này hiện không bị khóa");

        _context.LocationLocks.Remove(lockObj);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Mở khóa vị trí kệ thành công" });
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> AdjustInventory([FromBody] AdjustInventoryRequestDto dto)
    {
        if (!await HasPermissionAsync("exception_framework_mvp.approve") && !await HasPermissionAsync("Inventory.Movements.Create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var existingTx = await _context.InventoryTransactions
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.TraceId == dto.IdempotencyKey);
        if (existingTx != null)
        {
            return Ok(new { message = "Inventory adjusted successfully (idempotent)" });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var inventory = await _context.Inventories.FirstOrDefaultAsync(inv =>
                inv.TenantId == tenantId &&
                inv.LocationId == dto.LocationId &&
                inv.ItemId == dto.ItemId &&
                inv.LotNo == dto.LotNo);

            if (dto.Qty > 0)
            {
                if (inventory == null)
                {
                    inventory = new Entities.Inventory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        LocationId = dto.LocationId,
                        ItemId = dto.ItemId,
                        LotNo = dto.LotNo,
                        QtyOnHand = dto.Qty,
                        QtyReserved = 0,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username,
                        RowVersion = 1
                    };
                    _context.Inventories.Add(inventory);
                }
                else
                {
                    inventory.QtyOnHand += dto.Qty;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventory.UpdatedBy = username;
                    inventory.RowVersion += 1;
                }

                var ledger = new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = dto.ItemId,
                    LotNo = dto.LotNo,
                    LocationId = dto.LocationId,
                    TransactionType = "ADJ_IN",
                    Qty = dto.Qty,
                    TraceId = dto.IdempotencyKey,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                _context.InventoryTransactions.Add(ledger);
            }
            else if (dto.Qty < 0)
            {
                var absQty = Math.Abs(dto.Qty);
                if (inventory == null)
                {
                    return BadRequest(new { errorCode = "INVENTORY_RECORD_MISSED", message = "Không tìm thấy dòng tồn kho để điều chỉnh giảm" });
                }

                var availableQty = inventory.QtyOnHand - inventory.QtyReserved;
                if (availableQty < absQty)
                {
                    return BadRequest(new { errorCode = "INSUFFICIENT_AVAILABLE_STOCK", message = $"Không đủ tồn kho khả dụng để giảm {absQty} sản phẩm." });
                }

                inventory.QtyOnHand -= absQty;
                inventory.UpdatedAt = DateTime.UtcNow;
                inventory.UpdatedBy = username;
                inventory.RowVersion += 1;

                if (inventory.QtyOnHand == 0 && inventory.QtyReserved == 0)
                {
                    _context.Inventories.Remove(inventory);
                }

                var ledger = new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = dto.ItemId,
                    LotNo = dto.LotNo,
                    LocationId = dto.LocationId,
                    TransactionType = "ADJ_OUT",
                    Qty = dto.Qty,
                    TraceId = dto.IdempotencyKey,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                _context.InventoryTransactions.Add(ledger);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Inventory adjusted successfully" });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return StatusCode(409, new { errorCode = "CONCURRENCY_CONFLICT", message = "Dữ liệu tồn kho đã thay đổi bởi phiên làm việc khác, vui lòng tải lại trang" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
