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

namespace Nexustock.Modules.Inventory.Controllers;

[Authorize]
[ApiController]
[Route("api/stocktakes")]
public class StocktakeController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly MasterDataDbContext _masterContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;

    public StocktakeController(
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

    [HttpGet]
    public async Task<IActionResult> GetStocktakes()
    {
        if (!await HasPermissionAsync("Inventory.CycleCount.View")) return Forbid();

        var tenantId = GetTenantId();
        var stocktakes = await _context.Stocktakes
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var zoneIds = stocktakes.Where(s => s.ZoneId.HasValue).Select(s => s.ZoneId!.Value).Distinct().ToList();
        var zones = await _masterContext.StorageZones
            .Where(z => zoneIds.Contains(z.Id))
            .ToDictionaryAsync(z => z.Id, z => z.Name);

        var response = stocktakes.Select(s => new StocktakeListResponseDto
        {
            Id = s.Id,
            StocktakeNo = s.StocktakeNo,
            Status = s.Status,
            ZoneId = s.ZoneId,
            ZoneName = s.ZoneId.HasValue && zones.TryGetValue(s.ZoneId.Value, out var name) ? name : "Toàn kho / Tùy chọn",
            TotalVarianceAmount = s.TotalVarianceAmount,
            CurrentApprovalLevel = s.CurrentApprovalLevel,
            StartedAt = s.StartedAt,
            StartedBy = s.StartedBy,
            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy
        }).ToList();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStocktakeDetails(Guid id)
    {
        if (!await HasPermissionAsync("Inventory.CycleCount.View")) return Forbid();

        var tenantId = GetTenantId();
        var stocktake = await _context.Stocktakes.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (stocktake == null) return NotFound("Không tìm thấy đợt kiểm kê");

        var items = await _context.StocktakeItems.Where(i => i.StocktakeId == id).ToListAsync();

        var productIds = items.Select(i => i.ItemId).Distinct().ToList();
        var products = await _masterContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Code });

        var locationIds = items.Select(i => i.LocationId).Distinct().ToList();
        var locations = await _masterContext.StorageLocations
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code);

        var itemDtos = items.Select(i => new
        {
            i.Id,
            i.LocationId,
            LocationCode = locations.TryGetValue(i.LocationId, out var locCode) ? locCode : "Unknown",
            i.ItemId,
            ItemName = products.TryGetValue(i.ItemId, out var p) ? p.Name : "Unknown",
            ItemCode = products.TryGetValue(i.ItemId, out var p2) ? p2.Code : "Unknown",
            i.LotNo,
            i.SystemQty,
            i.CountedQty,
            i.VarianceQty,
            i.Status
        }).ToList();

        return Ok(new { stocktake, items = itemDtos });
    }

    [HttpPost]
    public async Task<IActionResult> CreateStocktake([FromBody] CreateStocktakeRequestDto dto)
    {
        if (!await HasPermissionAsync("Inventory.CycleCount.Create")) return Forbid();

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var dup = await _context.Stocktakes.AnyAsync(s => s.StocktakeNo == dto.StocktakeNo && s.TenantId == tenantId);
        if (dup) return BadRequest(new { errorCode = "DUPLICATE_STOCKTAKE_NO", message = "Mã đợt kiểm kê đã tồn tại" });

        var stocktake = new Stocktake
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StocktakeNo = dto.StocktakeNo,
            Status = "Draft",
            ZoneId = dto.ZoneId,
            TotalVarianceAmount = 0,
            CurrentApprovalLevel = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };

        _context.Stocktakes.Add(stocktake);
        await _context.SaveChangesAsync();

        return Ok(new { id = stocktake.Id, message = "Tạo đợt kiểm kê nháp thành công" });
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartStocktake(Guid id)
    {
        if (!await HasPermissionAsync("Inventory.CycleCount.Create")) return Forbid();

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var stocktake = await _context.Stocktakes.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (stocktake == null) return NotFound("Không tìm thấy đợt kiểm kê");
        if (stocktake.Status != "Draft") return BadRequest(new { errorCode = "INVALID_STATUS", message = "Đợt kiểm kê không ở trạng thái nháp" });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var targetLocations = await _masterContext.StorageLocations
                .Where(l => l.TenantId == tenantId && (!stocktake.ZoneId.HasValue || l.ZoneId == stocktake.ZoneId.Value))
                .Select(l => new { l.Id, l.Code })
                .ToListAsync();

            var locationIds = targetLocations.Select(l => l.Id).ToList();

            var currentStock = await _context.Inventories
                .Where(i => i.TenantId == tenantId && locationIds.Contains(i.LocationId))
                .ToListAsync();

            foreach (var stock in currentStock)
            {
                var item = new StocktakeItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    StocktakeId = stocktake.Id,
                    LocationId = stock.LocationId,
                    ItemId = stock.ItemId,
                    LotNo = stock.LotNo,
                    SystemQty = stock.QtyOnHand,
                    CountedQty = null,
                    VarianceQty = null,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                _context.StocktakeItems.Add(item);
            }

            foreach (var loc in targetLocations)
            {
                var alreadyLocked = await _context.LocationLocks.AnyAsync(l => l.TenantId == tenantId && l.LocationId == loc.Id);
                if (!alreadyLocked)
                {
                    var locLock = new LocationLock
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        LocationId = loc.Id,
                        LockType = "ALL",
                        ReasonCode = "STOCKTAKE",
                        LockedBy = username,
                        LockedAt = DateTime.UtcNow
                    };
                    _context.LocationLocks.Add(locLock);
                }
            }

            stocktake.Status = "Counting";
            stocktake.StartedAt = DateTime.UtcNow;
            stocktake.StartedBy = username;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Đã bắt đầu kiểm kê và phong tỏa các vị trí kệ" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("{id:guid}/count")]
    public async Task<IActionResult> RecordCount(Guid id, [FromBody] RecordCountRequestDto dto)
    {
        if (!await HasPermissionAsync("Inventory.CycleCount.Count")) return Forbid();

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var stocktake = await _context.Stocktakes.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (stocktake == null) return NotFound("Không tìm thấy đợt kiểm kê");
        if (stocktake.Status != "Counting") return BadRequest(new { errorCode = "INVALID_STATUS", message = "Đợt kiểm kê không ở trạng thái Counting" });

        var item = await _context.StocktakeItems.FirstOrDefaultAsync(i =>
            i.StocktakeId == id &&
            i.LocationId == dto.LocationId &&
            i.ItemId == dto.ItemId &&
            i.LotNo == dto.LotNo);

        if (item == null)
        {
            item = new StocktakeItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StocktakeId = id,
                LocationId = dto.LocationId,
                ItemId = dto.ItemId,
                LotNo = dto.LotNo,
                SystemQty = 0,
                CountedQty = dto.CountedQty,
                VarianceQty = dto.CountedQty,
                Status = "Counted",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.StocktakeItems.Add(item);
        }
        else
        {
            item.CountedQty = dto.CountedQty;
            item.VarianceQty = dto.CountedQty - item.SystemQty;
            item.Status = "Counted";
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = username;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Ghi nhận kết quả đếm thành công" });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveStocktake(Guid id, [FromBody] ApproveStocktakeRequestDto dto)
    {
        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";
        var traceId = HttpContext.TraceIdentifier;

        var stocktake = await _context.Stocktakes.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (stocktake == null) return NotFound("Không tìm thấy đợt kiểm kê");

        var items = await _context.StocktakeItems.Where(i => i.StocktakeId == id).ToListAsync();

        // 1. Phân tích giá trị chênh lệch khi đợt kiểm từ Counting chuyển sang Approve Flow
        if (stocktake.Status == "Counting")
        {
            if (items.Any(i => i.Status == "Pending"))
            {
                return BadRequest(new { errorCode = "PENDING_ITEMS", message = "Vẫn còn sản phẩm chưa được kiểm đếm" });
            }

            decimal totalVarianceAmount = 0;
            foreach (var item in items)
            {
                var varianceQty = item.VarianceQty ?? 0;
                var standardCost = 500000m; // Giả định giá mặc định của vật tư 500k VNĐ
                totalVarianceAmount += Math.Abs(varianceQty) * standardCost;
            }

            stocktake.TotalVarianceAmount = totalVarianceAmount;

            // Xác định cấp duyệt
            if (totalVarianceAmount < 10000000m) // < 10 triệu
            {
                stocktake.Status = "Pending_L1_Approve";
                stocktake.CurrentApprovalLevel = 1;
            }
            else if (totalVarianceAmount < 100000000m) // 10M - 100M
            {
                stocktake.Status = "Pending_L2_Approve";
                stocktake.CurrentApprovalLevel = 2;
            }
            else // > 100M
            {
                stocktake.Status = "Pending_L3_Approve";
                stocktake.CurrentApprovalLevel = 3;
            }

            await _context.SaveChangesAsync();
            return Ok(new { status = stocktake.Status, totalVarianceAmount = stocktake.TotalVarianceAmount, message = $"Đã tính toán giá trị chênh lệch và chuyển sang chờ duyệt cấp L{stocktake.CurrentApprovalLevel}" });
        }

        // 2. Kiểm tra thẩm quyền duyệt của User hiện tại dựa trên trạng thái
        var currentStatus = stocktake.Status;
        if (currentStatus == "Pending_L1_Approve")
        {
            if (!await HasPermissionAsync("Inventory.CycleCount.Approve.L1") && 
                !await HasPermissionAsync("Inventory.CycleCount.Approve.L2") && 
                !await HasPermissionAsync("Inventory.CycleCount.Approve.L3"))
            {
                return Forbid();
            }
        }
        else if (currentStatus == "Pending_L2_Approve")
        {
            if (!await HasPermissionAsync("Inventory.CycleCount.Approve.L2") && 
                !await HasPermissionAsync("Inventory.CycleCount.Approve.L3"))
            {
                return Forbid();
            }
        }
        else if (currentStatus == "Pending_L3_Approve")
        {
            if (!await HasPermissionAsync("Inventory.CycleCount.Approve.L3"))
            {
                return Forbid();
            }
        }
        else
        {
            return BadRequest(new { errorCode = "INVALID_STATUS", message = "Trạng thái đợt kiểm kê không hợp lệ để duyệt" });
        }

        // 3. Thực thi áp dụng điều chỉnh tồn kho (Chỉ cấp duyệt đủ thẩm quyền mới thực thi)
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var hasVariance = items.Any(i => i.VarianceQty != 0);

            if (hasVariance)
            {
                var adjustment = new StockAdjustment
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    StocktakeId = id,
                    AdjustmentNo = $"ADJ-{stocktake.StocktakeNo}",
                    Status = "Applied",
                    ApprovedAt = DateTime.UtcNow,
                    ApprovedBy = username,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                _context.StockAdjustments.Add(adjustment);

                foreach (var item in items.Where(i => i.VarianceQty != 0))
                {
                    var variance = item.VarianceQty ?? 0;

                    var adjItem = new StockAdjustmentItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        AdjustmentId = adjustment.Id,
                        LocationId = item.LocationId,
                        ItemId = item.ItemId,
                        LotNo = item.LotNo,
                        BeforeQty = item.SystemQty,
                        AfterQty = item.CountedQty ?? 0,
                        DeltaQty = variance,
                        ReasonCode = dto.ReasonCode,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username
                    };
                    _context.StockAdjustmentItems.Add(adjItem);

                    var inventory = await _context.Inventories.FirstOrDefaultAsync(inv =>
                        inv.TenantId == tenantId &&
                        inv.LocationId == item.LocationId &&
                        inv.ItemId == item.ItemId &&
                        inv.LotNo == item.LotNo);

                    if (variance > 0)
                    {
                        if (inventory == null)
                        {
                            inventory = new Entities.Inventory
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                LocationId = item.LocationId,
                                ItemId = item.ItemId,
                                LotNo = item.LotNo,
                                QtyOnHand = variance,
                                QtyReserved = 0,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = username
                            };
                            _context.Inventories.Add(inventory);
                        }
                        else
                        {
                            inventory.QtyOnHand += variance;
                            inventory.UpdatedAt = DateTime.UtcNow;
                            inventory.UpdatedBy = username;
                        }

                        var ledger = new InventoryTransaction
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            ItemId = item.ItemId,
                            LotNo = item.LotNo,
                            LocationId = item.LocationId,
                            TransactionType = "ADJ_IN",
                            Qty = variance,
                            TraceId = traceId,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = username
                        };
                        _context.InventoryTransactions.Add(ledger);
                    }
                    else
                    {
                        if (inventory == null)
                        {
                            return BadRequest(new { errorCode = "INVENTORY_RECORD_MISSED", message = "Không tìm thấy dòng tồn kho để điều chỉnh giảm" });
                        }

                        var availableQty = inventory.QtyOnHand - inventory.QtyReserved;
                        var absVariance = Math.Abs(variance);
                        if (availableQty < absVariance)
                        {
                            return BadRequest(new { errorCode = "INSUFFICIENT_AVAILABLE_STOCK", message = $"Vật tư đang được giữ hàng để xuất. Không đủ tồn kho khả dụng để giảm {absVariance} sản phẩm." });
                        }

                        inventory.QtyOnHand -= absVariance;
                        inventory.UpdatedAt = DateTime.UtcNow;
                        inventory.UpdatedBy = username;

                        if (inventory.QtyOnHand == 0 && inventory.QtyReserved == 0)
                        {
                            _context.Inventories.Remove(inventory);
                        }

                        var ledger = new InventoryTransaction
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            ItemId = item.ItemId,
                            LotNo = item.LotNo,
                            LocationId = item.LocationId,
                            TransactionType = "ADJ_OUT",
                            Qty = variance,
                            TraceId = traceId,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = username
                        };
                        _context.InventoryTransactions.Add(ledger);
                    }
                }
            }

            var locationIds = items.Select(i => i.LocationId).Distinct().ToList();
            var locks = await _context.LocationLocks
                .Where(l => l.TenantId == tenantId && locationIds.Contains(l.LocationId) && l.ReasonCode == "STOCKTAKE")
                .ToListAsync();
            _context.LocationLocks.RemoveRange(locks);

            stocktake.Status = "Approved";
            stocktake.CompletedAt = DateTime.UtcNow;
            stocktake.CompletedBy = username;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Phê duyệt đợt kiểm kê và áp dụng điều chỉnh thành công" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
