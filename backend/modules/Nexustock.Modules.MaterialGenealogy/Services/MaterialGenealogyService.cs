using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.MaterialGenealogy.Contexts;
using Nexustock.Modules.MaterialGenealogy.Entities;
using Nexustock.Modules.MaterialGenealogy.DTOs;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Serial.Contexts;
using Nexustock.Modules.Serial.Entities;
using Nexustock.Modules.MasterData.Contexts;
using System.Text.Json;

namespace Nexustock.Modules.MaterialGenealogy.Services;

public class MaterialGenealogyService : IMaterialGenealogyService
{
    private readonly MaterialGenealogyDbContext _context;
    private readonly InventoryDbContext _inventoryContext;
    private readonly SerialDbContext _serialContext;
    private readonly MasterDataDbContext _masterContext;

    public MaterialGenealogyService(
        MaterialGenealogyDbContext context,
        InventoryDbContext inventoryContext,
        SerialDbContext serialContext,
        MasterDataDbContext masterContext)
    {
        _context = context;
        _inventoryContext = inventoryContext;
        _serialContext = serialContext;
        _masterContext = masterContext;
    }

    public async Task CreateRelationAsync(Guid tenantId, string username, CreateLotRelationDto dto)
    {
        var parentLot = await _inventoryContext.Lots
            .FirstOrDefaultAsync(l => l.LotNo == dto.ParentLotNo && l.TenantId == tenantId);
        var childLot = await _inventoryContext.Lots
            .FirstOrDefaultAsync(l => l.LotNo == dto.ChildLotNo && l.TenantId == tenantId);

        if (parentLot == null || childLot == null)
            throw new KeyNotFoundException("Không tìm thấy thông tin Lot cha hoặc Lot con.");

        // 1. Kiểm tra chu kỳ (DFS)
        await VerifyNoCycleAsync(tenantId, parentLot.Id, childLot.Id);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 2. Lưu liên kết phả hệ
            var relation = new LotRelation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ParentLotId = parentLot.Id,
                ChildLotId = childLot.Id,
                RelationType = dto.RelationType,
                QtyTransferred = dto.QtyTransferred,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            await _context.LotRelations.AddAsync(relation);

            // 3. Ghi nhận sự kiện phả hệ
            var evt = new GenealogyEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventType = dto.RelationType,
                LotId = childLot.Id,
                Description = $"Tạo liên kết Lot từ {dto.ParentLotNo} sang {dto.ChildLotNo}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username,
                Payload = JsonSerializer.Serialize(new { dto.QtyTransferred, dto.SerialNos })
            };
            await _context.GenealogyEvents.AddAsync(evt);

            // 4. Đồng bộ số dư Tồn kho
            var parentInv = await _inventoryContext.Inventories
                .FirstOrDefaultAsync(i => i.LotNo == dto.ParentLotNo && i.TenantId == tenantId);
            if (parentInv == null || parentInv.QtyOnHand < dto.QtyTransferred)
                throw new InvalidOperationException("Không đủ tồn kho khả dụng tại Lot cha để thực hiện chia tách.");

            parentInv.QtyOnHand -= dto.QtyTransferred;
            parentInv.UpdatedAt = DateTime.UtcNow;
            parentInv.UpdatedBy = username;

            var childInv = await _inventoryContext.Inventories
                .FirstOrDefaultAsync(i => i.LotNo == dto.ChildLotNo && i.TenantId == tenantId);
            if (childInv == null)
            {
                childInv = new Inventory.Entities.Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = parentInv.ItemId,
                    LocationId = parentInv.LocationId,
                    LotNo = dto.ChildLotNo,
                    QtyOnHand = dto.QtyTransferred,
                    QtyReserved = 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                await _inventoryContext.Inventories.AddAsync(childInv);
            }
            else
            {
                childInv.QtyOnHand += dto.QtyTransferred;
                childInv.UpdatedAt = DateTime.UtcNow;
                childInv.UpdatedBy = username;
            }

            // 5. Ghi nhận sự kiện Serial (SerialEvent) nếu có
            if (dto.SerialNos != null && dto.SerialNos.Any())
            {
                var serials = await _serialContext.SerialNumbers
                    .Where(s => dto.SerialNos.Contains(s.SerialNo) && s.TenantId == tenantId)
                    .ToListAsync();

                foreach (var s in serials)
                {
                    s.UpdatedAt = DateTime.UtcNow;
                    s.UpdatedBy = username;

                    var serialEvent = new SerialEvent
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        SerialId = s.Id,
                        EventType = "GENEALOGY_" + dto.RelationType,
                        FromLocationId = s.LocationId,
                        ToLocationId = s.LocationId,
                        ReferenceId = childLot.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username
                    };
                    await _serialContext.SerialEvents.AddAsync(serialEvent);
                }
            }

            await _context.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await _serialContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<LotGenealogyNodeDto> GetLotTreeAsync(Guid tenantId, string lotNo)
    {
        var lot = await _inventoryContext.Lots
            .FirstOrDefaultAsync(l => l.LotNo == lotNo && l.TenantId == tenantId);
        if (lot == null) throw new KeyNotFoundException("Không tìm thấy Lot.");

        var product = await _masterContext.Products.FirstOrDefaultAsync(p => p.Id == lot.ItemId);
        var inv = await _inventoryContext.Inventories
            .FirstOrDefaultAsync(i => i.LotNo == lotNo && i.TenantId == tenantId);

        var node = new LotGenealogyNodeDto
        {
            LotId = lot.Id,
            LotNo = lot.LotNo,
            ProductCode = product?.Code ?? "Unknown",
            ProductName = product?.Name ?? "Unknown",
            QtyOnHand = inv?.QtyOnHand ?? 0,
            Status = lot.QcStatus
        };

        // Lấy danh sách quan hệ con cháu trực tiếp
        var childRelations = await _context.LotRelations
            .Where(r => r.ParentLotId == lot.Id && r.TenantId == tenantId)
            .ToListAsync();

        foreach (var rel in childRelations)
        {
            var childLot = await _inventoryContext.Lots.FirstOrDefaultAsync(l => l.Id == rel.ChildLotId);
            if (childLot != null)
            {
                var childNode = await GetLotTreeAsync(tenantId, childLot.LotNo);
                node.Children.Add(childNode);
            }
        }

        return node;
    }

    public async Task HoldBranchAsync(Guid tenantId, string username, HoldBranchDto dto)
    {
        var startLot = await _inventoryContext.Lots
            .FirstOrDefaultAsync(l => l.LotNo == dto.TargetLotNo && l.TenantId == tenantId);
        if (startLot == null) throw new KeyNotFoundException("Không tìm thấy Lot mục tiêu.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var descendantLotIds = new List<Guid>();
            var queue = new Queue<(Guid Id, int Depth)>();
            queue.Enqueue((startLot.Id, 1));

            while (queue.Any())
            {
                var (currentId, depth) = queue.Dequeue();
                descendantLotIds.Add(currentId);

                if (depth >= 50) continue; // Chốt chặn độ sâu phòng ngừa treo hệ thống

                var children = await _context.LotRelations
                    .Where(r => r.ParentLotId == currentId && r.TenantId == tenantId)
                    .Select(r => r.ChildLotId)
                    .ToListAsync();

                foreach (var c in children)
                {
                    if (!descendantLotIds.Contains(c) && queue.All(x => x.Id != c))
                        queue.Enqueue((c, depth + 1));
                }
            }

            var lotsToUpdate = await _inventoryContext.Lots
                .Where(l => descendantLotIds.Contains(l.Id) && l.TenantId == tenantId)
                .ToListAsync();

            foreach (var lot in lotsToUpdate)
            {
                lot.QcStatus = "HOLD";

                var evt = new GenealogyEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EventType = "HOLD_BRANCH",
                    LotId = lot.Id,
                    Description = $"Phong tỏa nhánh Lot từ gốc {dto.TargetLotNo}. Lý do: {dto.Description}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                await _context.GenealogyEvents.AddAsync(evt);
            }

            await _context.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task VerifyNoCycleAsync(Guid tenantId, Guid parentLotId, Guid childLotId)
    {
        if (parentLotId == childLotId)
            throw new InvalidOperationException("Không thể tạo liên kết cha con với cùng một Lot.");

        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentLotId);

        while (queue.Any())
        {
            var current = queue.Dequeue();
            if (current == childLotId)
                throw new InvalidOperationException("Phát hiện chu kỳ phả hệ (Cycle detected)! Lot con không thể là tổ tiên của Lot cha.");

            if (!visited.Contains(current))
            {
                visited.Add(current);
                var parents = await _context.LotRelations
                    .Where(r => r.ChildLotId == current && r.TenantId == tenantId)
                    .Select(r => r.ParentLotId)
                    .ToListAsync();

                foreach (var p in parents)
                {
                    queue.Enqueue(p);
                }
            }
        }
    }
}
