using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.Lpn.Dtos;
using Nexustock.Modules.Lpn.Entities;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.Modules.Lpn.Services;

public class LpnService : ILpnService
{
    private readonly LpnDbContext _dbContext;
    private readonly InventoryDbContext _inventoryContext;
    private readonly MasterDataDbContext _masterDataContext;

    public LpnService(
        LpnDbContext dbContext,
        InventoryDbContext inventoryContext,
        MasterDataDbContext masterDataContext)
    {
        _dbContext = dbContext;
        _inventoryContext = inventoryContext;
        _masterDataContext = masterDataContext;
    }

    public async Task<LpnDto> CreateLpnAsync(Guid tenantId, CreateLpnDto dto, string username)
    {
        // Kiểm tra xem vị trí kệ có tồn tại không
        var locationExists = await _masterDataContext.StorageLocations
            .AnyAsync(l => l.Id == dto.LocationId);
        if (!locationExists)
        {
            throw new Exception("Vị trí kệ chỉ định không tồn tại.");
        }

        // Kiểm tra xem mã LPN đã tồn tại chưa
        var lpnExists = await _dbContext.Lpns
            .AnyAsync(l => l.TenantId == tenantId && l.LpnNo.ToLower() == dto.LpnNo.ToLower());
        if (lpnExists)
        {
            throw new Exception($"Mã LPN {dto.LpnNo} đã tồn tại trong hệ thống.");
        }

        var lpn = new Entities.Lpn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LpnNo = dto.LpnNo,
            LocationId = dto.LocationId,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };

        await _dbContext.Lpns.AddAsync(lpn);

        // Ghi nhận sự kiện
        var lpnEvent = new LpnEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LpnId = lpn.Id,
            EventType = "CREATE",
            FromLocationId = dto.LocationId,
            ToLocationId = dto.LocationId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };
        await _dbContext.LpnEvents.AddAsync(lpnEvent);

        await _dbContext.SaveChangesAsync();
        return MapToDto(lpn);
    }

    public async Task<bool> AttachToLpnAsync(Guid tenantId, Guid lpnId, AttachLpnDto dto, string username)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            // 1. Lock LPN
            var lpn = await _dbContext.Lpns
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == lpnId);
            if (lpn == null || lpn.Status != "ACTIVE")
            {
                throw new Exception("LPN không tồn tại hoặc không ở trạng thái ACTIVE.");
            }

            // 2. Tìm dòng tồn kho tự do (chưa thuộc LPN nào) tại vị trí của LPN
            var sourceInv = await _inventoryContext.Inventories
                .FirstOrDefaultAsync(i => i.TenantId == tenantId
                                       && i.LocationId == lpn.LocationId
                                       && i.ItemId == dto.ItemId
                                       && i.LotNo == dto.LotNo
                                       && i.LpnId == null);

            if (sourceInv == null)
            {
                throw new Exception("Không tìm thấy tồn kho tự do cho vật tư/lô hàng chỉ định tại vị trí kệ này.");
            }

            decimal availableQty = sourceInv.QtyOnHand - sourceInv.QtyReserved;
            if (availableQty < dto.Qty)
            {
                throw new Exception($"Không đủ tồn kho tự do khả dụng. Cần: {dto.Qty}, Khả dụng: {availableQty}.");
            }

            // 3. Tách dòng tồn kho (Split Row)
            if (sourceInv.QtyOnHand > dto.Qty)
            {
                // Chia tách theo tỷ lệ nếu dòng tồn kho đang bị khóa giữ một phần
                decimal ratio = dto.Qty / sourceInv.QtyOnHand;
                decimal reservedToMove = Math.Round(ratio * sourceInv.QtyReserved, 6);

                sourceInv.QtyOnHand -= dto.Qty;
                sourceInv.QtyReserved = Math.Max(0, sourceInv.QtyReserved - reservedToMove);

                var newInv = new Nexustock.Modules.Inventory.Entities.Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = dto.ItemId,
                    LotNo = dto.LotNo,
                    LocationId = lpn.LocationId,
                    LpnId = lpn.Id,
                    QtyOnHand = dto.Qty,
                    QtyReserved = reservedToMove,
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                };

                await _inventoryContext.Inventories.AddAsync(newInv);
            }
            else
            {
                // Gán toàn bộ dòng tồn kho vào LPN
                sourceInv.LpnId = lpn.Id;
            }

            // 4. Lưu LpnEvent
            var lpnEvent = new LpnEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LpnId = lpn.Id,
                EventType = "ATTACH",
                ItemId = dto.ItemId,
                LotNo = dto.LotNo,
                Qty = dto.Qty,
                FromLocationId = lpn.LocationId,
                ToLocationId = lpn.LocationId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            await _dbContext.LpnEvents.AddAsync(lpnEvent);

            await _dbContext.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DetachFromLpnAsync(Guid tenantId, Guid lpnId, DetachLpnDto dto, string username)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            // 1. Lock LPN
            var lpn = await _dbContext.Lpns
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == lpnId);
            if (lpn == null)
            {
                throw new Exception("LPN không tồn tại.");
            }

            // 2. Tìm dòng tồn kho thuộc LPN này
            var sourceInv = await _inventoryContext.Inventories
                .FirstOrDefaultAsync(i => i.TenantId == tenantId
                                       && i.LpnId == lpn.Id
                                       && i.ItemId == dto.ItemId
                                       && i.LotNo == dto.LotNo);

            if (sourceInv == null || sourceInv.QtyOnHand < dto.Qty)
            {
                throw new Exception("Không đủ tồn kho tương ứng trên LPN.");
            }

            // 3. Tách dòng tồn kho (Split Row)
            if (sourceInv.QtyOnHand > dto.Qty)
            {
                // Chia tách tỷ lệ
                decimal ratio = dto.Qty / sourceInv.QtyOnHand;
                decimal reservedToMove = Math.Round(ratio * sourceInv.QtyReserved, 6);

                sourceInv.QtyOnHand -= dto.Qty;
                sourceInv.QtyReserved = Math.Max(0, sourceInv.QtyReserved - reservedToMove);

                var newInv = new Nexustock.Modules.Inventory.Entities.Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = dto.ItemId,
                    LotNo = dto.LotNo,
                    LocationId = lpn.LocationId,
                    LpnId = null, // Tách tự do
                    QtyOnHand = dto.Qty,
                    QtyReserved = reservedToMove,
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                };

                await _inventoryContext.Inventories.AddAsync(newInv);
            }
            else
            {
                // Rút toàn bộ dòng khỏi LPN
                sourceInv.LpnId = null;
            }

            // 4. Lưu LpnEvent
            var lpnEvent = new LpnEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LpnId = lpn.Id,
                EventType = "DETACH",
                ItemId = dto.ItemId,
                LotNo = dto.LotNo,
                Qty = dto.Qty,
                FromLocationId = lpn.LocationId,
                ToLocationId = lpn.LocationId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            await _dbContext.LpnEvents.AddAsync(lpnEvent);

            await _dbContext.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> MoveLpnAsync(Guid tenantId, Guid lpnId, MoveLpnDto dto, string username)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            // 1. Lock LPN
            var lpn = await _dbContext.Lpns
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == lpnId);
            if (lpn == null)
            {
                throw new Exception("LPN không tồn tại.");
            }

            Guid oldLocationId = lpn.LocationId;
            if (oldLocationId == dto.TargetLocationId)
            {
                return true; // Đã ở đúng vị trí
            }

            // 2. Kiểm tra vị trí kệ đích có tồn tại không
            var targetLoc = await _masterDataContext.StorageLocations
                .FirstOrDefaultAsync(l => l.Id == dto.TargetLocationId);
            if (targetLoc == null)
            {
                throw new Exception("Kệ đích không tồn tại.");
            }

            // 3. Lấy toàn bộ inventories trên LPN này
            var inventories = await _inventoryContext.Inventories
                .Where(i => i.TenantId == tenantId && i.LpnId == lpn.Id)
                .ToListAsync();

            // 4. Cập nhật vị trí LPN và inventories
            lpn.LocationId = dto.TargetLocationId;
            lpn.UpdatedAt = DateTime.UtcNow;
            lpn.UpdatedBy = username;

            foreach (var inv in inventories)
            {
                // Ghi nhận dòng InventoryMovement
                var movement = new InventoryMovement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = inv.ItemId,
                    LotNo = inv.LotNo,
                    FromLocationId = oldLocationId,
                    ToLocationId = dto.TargetLocationId,
                    Qty = inv.QtyOnHand,
                    Status = "Completed",
                    ReasonCode = "LPN_MOVE",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                await _inventoryContext.InventoryMovements.AddAsync(movement);

                inv.LocationId = dto.TargetLocationId;
                inv.UpdatedAt = DateTime.UtcNow;
                inv.UpdatedBy = username;
            }

            // 5. Lưu LpnEvent
            var lpnEvent = new LpnEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LpnId = lpn.Id,
                EventType = "MOVE",
                FromLocationId = oldLocationId,
                ToLocationId = dto.TargetLocationId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            await _dbContext.LpnEvents.AddAsync(lpnEvent);

            await _dbContext.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<LpnDto>> GetLpnsAsync(Guid tenantId)
    {
        var lpns = await _dbContext.Lpns
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return lpns.Select(MapToDto).ToList();
    }

    public async Task<List<LpnEventDto>> GetLpnEventsAsync(Guid tenantId, Guid lpnId)
    {
        var events = await _dbContext.LpnEvents
            .Where(e => e.TenantId == tenantId && e.LpnId == lpnId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var products = await _masterDataContext.Products.ToDictionaryAsync(p => p.Id, p => p);
        var locations = await _masterDataContext.StorageLocations.ToDictionaryAsync(l => l.Id, l => l);

        return events.Select(e => new LpnEventDto
        {
            Id = e.Id,
            EventType = e.EventType,
            ItemId = e.ItemId,
            ItemCode = e.ItemId.HasValue && products.TryGetValue(e.ItemId.Value, out var p) ? p.Code : null,
            ItemName = e.ItemId.HasValue && products.TryGetValue(e.ItemId.Value, out p) ? p.Name : null,
            LotNo = e.LotNo,
            Qty = e.Qty,
            FromLocationCode = e.FromLocationId.HasValue && locations.TryGetValue(e.FromLocationId.Value, out var l) ? l.Code : null,
            ToLocationCode = e.ToLocationId.HasValue && locations.TryGetValue(e.ToLocationId.Value, out l) ? l.Code : null,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy
        }).ToList();
    }

    private LpnDto MapToDto(Entities.Lpn lpn)
    {
        return new LpnDto
        {
            Id = lpn.Id,
            LpnNo = lpn.LpnNo,
            LocationId = lpn.LocationId,
            Status = lpn.Status,
            CreatedAt = lpn.CreatedAt
        };
    }
}
