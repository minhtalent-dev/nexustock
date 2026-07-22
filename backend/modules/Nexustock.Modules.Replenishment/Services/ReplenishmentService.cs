using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Replenishment.Contexts;
using Nexustock.Modules.Replenishment.Entities;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Exceptions.Entities;
using Nexustock.Modules.Qc.Abstractions;

namespace Nexustock.Modules.Replenishment.Services;

public class ReplenishmentService : IReplenishmentService
{
    private readonly ReplenishmentDbContext _replenishmentDb;
    private readonly InventoryDbContext _inventoryDb;
    private readonly ExceptionsDbContext _exceptionsDb;
    private readonly IQcGateService _qcGate;

    public ReplenishmentService(
        ReplenishmentDbContext replenishmentDb,
        InventoryDbContext inventoryDb,
        ExceptionsDbContext exceptionsDb,
        IQcGateService qcGate)
    {
        _replenishmentDb = replenishmentDb;
        _inventoryDb = inventoryDb;
        _exceptionsDb = exceptionsDb;
        _qcGate = qcGate;
    }

    public async Task<List<ReplenishmentTask>> GenerateTasksAsync(Guid tenantId, string strategy = "FEFO")
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var generatedTasks = new List<ReplenishmentTask>();

        // 1. Lấy tất cả rules bổ sung của tenant
        var rules = await _replenishmentDb.ReplenishmentRules
            .Where(r => r.TenantId == tenantId)
            .ToListAsync();

        // Lấy danh sách LocationLocks để loại trừ các kệ đang bị khóa
        var lockedLocations = await _inventoryDb.LocationLocks
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.LocationId)
            .ToListAsync();

        foreach (var rule in rules)
        {
            // 2. Tính toán tồn khả dụng hiện tại ở Pick Face
            var pickFaceInv = await _inventoryDb.Inventories
                .Where(i => i.TenantId == tenantId && i.ItemId == rule.ItemId && i.LocationId == rule.LocationId)
                .ToListAsync();

            decimal currentAvailable = pickFaceInv.Sum(i => i.QtyAvailable);

            // 3. Tính toán lượng hàng đang bổ sung "in-flight" về Pick Face này
            decimal inFlightQty = await _replenishmentDb.ReplenishmentTasks
                .Where(t => t.TenantId == tenantId 
                         && t.ItemId == rule.ItemId 
                         && t.TargetLocationId == rule.LocationId 
                         && (t.Status == "PENDING" || t.Status == "ASSIGNED"))
                .SumAsync(t => t.RequestedQty);

            decimal totalVirtualStock = currentAvailable + inFlightQty;

            // 4. Nếu tồn ảo dưới Min, kích hoạt sinh task bổ sung
            if (totalVirtualStock < rule.MinQty)
            {
                decimal neededQty = rule.MaxQty - totalVirtualStock;

                // Tìm các kệ Bulk/Reserve chứa Lot đã Release của Item này
                var bulkInventories = await _inventoryDb.Inventories
                    .Where(inv => inv.TenantId == tenantId
                               && inv.ItemId == rule.ItemId
                               && inv.LocationId != rule.LocationId
                               && inv.QtyOnHand - inv.QtyReserved > 0)
                    .ToListAsync();

                // Lọc bỏ kệ bị khóa
                bulkInventories = bulkInventories
                    .Where(inv => !lockedLocations.Contains(inv.LocationId))
                    .ToList();

                var lotNos = bulkInventories.Select(i => i.LotNo).Distinct().ToList();
                var lots = await _replenishmentDb.Lots
                    .Where(l => l.TenantId == tenantId && lotNos.Contains(l.LotNo) && l.QcStatus == "Release")
                    .ToListAsync();

                var candidates = (from inv in bulkInventories
                                  join lot in lots on inv.LotNo equals lot.LotNo
                                  select new { inv, lot }).ToList();

                // Sắp xếp theo FEFO hoặc FIFO
                if (strategy.Equals("FEFO", StringComparison.OrdinalIgnoreCase))
                {
                    candidates = candidates
                        .OrderBy(c => c.lot.ExpiryDate ?? DateTime.MaxValue)
                        .ThenBy(c => c.inv.CreatedAt)
                        .ThenBy(c => c.inv.Id)
                        .ToList();
                }
                else // FIFO
                {
                    candidates = candidates
                        .OrderBy(c => c.lot.ProductionDate ?? DateTime.MaxValue)
                        .ThenBy(c => c.inv.CreatedAt)
                        .ThenBy(c => c.inv.Id)
                        .ToList();
                }

                foreach (var candidate in candidates)
                {
                    if (neededQty <= 0) break;

                    decimal availableInBulk = candidate.inv.QtyOnHand - candidate.inv.QtyReserved;
                    if (availableInBulk <= 0) continue;

                    decimal allocatableQty = Math.Min(neededQty, availableInBulk);

                    // Tạo Replenishment Task
                    var repTask = new ReplenishmentTask
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ItemId = rule.ItemId,
                        SourceLocationId = candidate.inv.LocationId,
                        TargetLocationId = rule.LocationId,
                        LotNo = candidate.inv.LotNo,
                        RequestedQty = allocatableQty,
                        Status = "PENDING",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    // Khóa giữ (Reserve) tồn kho tại kệ Bulk nguồn
                    candidate.inv.QtyReserved += allocatableQty;

                    // Tạo MobileTask tương ứng cho Handheld
                    var mobileTask = new MobileTask
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ReferenceType = "REPLENISHMENT",
                        ReferenceId = repTask.Id,
                        LocationId = candidate.inv.LocationId, // Yêu cầu quét kệ nguồn trước
                        Step = "SCAN_SOURCE_LOC",
                        Status = "Open",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    repTask.MobileTaskId = mobileTask.Id;

                    _replenishmentDb.ReplenishmentTasks.Add(repTask);
                    _inventoryDb.MobileTasks.Add(mobileTask);
                    generatedTasks.Add(repTask);

                    neededQty -= allocatableQty;
                }
            }
        }

        if (generatedTasks.Any())
        {
            await _replenishmentDb.SaveChangesAsync();
            await _inventoryDb.SaveChangesAsync();
            scope.Complete();
        }

        return generatedTasks;
    }

    public async Task<ReplenishmentTask> CompleteTaskAsync(Guid taskId, decimal actualQty, string operatorName)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var task = await _replenishmentDb.ReplenishmentTasks
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            throw new ArgumentException("Task không tồn tại");

        if (task.Status == "COMPLETED" || task.Status == "CANCELLED")
            throw new InvalidOperationException("Task đã hoàn thành hoặc đã bị hủy");

        await _qcGate.EnsureLotUsableByLotNoAsync(task.TenantId, task.ItemId, task.LotNo);

        // RESOURCE ORDERING: Sắp xếp Location ID tăng dần để khóa hàng tránh deadlock
        var sortedLocations = new[] { task.SourceLocationId, task.TargetLocationId }
            .OrderBy(id => id)
            .ToArray();

        foreach (var locId in sortedLocations)
        {
            await _inventoryDb.Database.ExecuteSqlRawAsync(
                "SELECT id FROM inventories WHERE location_id = {0} AND item_id = {1} FOR UPDATE",
                locId, task.ItemId);
        }

        // Lấy thông tin tồn kho
        var sourceInv = await _inventoryDb.Inventories
            .FirstOrDefaultAsync(i => i.TenantId == task.TenantId && i.ItemId == task.ItemId && i.LocationId == task.SourceLocationId && i.LotNo == task.LotNo);

        if (sourceInv == null)
            throw new InvalidOperationException("Không tìm thấy số dư tồn kho tại kệ Bulk nguồn");

        var targetInv = await _inventoryDb.Inventories
            .FirstOrDefaultAsync(i => i.TenantId == task.TenantId && i.ItemId == task.ItemId && i.LocationId == task.TargetLocationId && i.LotNo == task.LotNo);

        // 1. Cập nhật tồn kho Bulk nguồn
        sourceInv.QtyOnHand -= actualQty;
        // Giải phóng 100% reservation ban đầu của task này
        sourceInv.QtyReserved = Math.Max(0, sourceInv.QtyReserved - task.RequestedQty);
        sourceInv.UpdatedAt = DateTime.UtcNow;
        sourceInv.UpdatedBy = operatorName;

        // 2. Cập nhật tồn kho Pick Face đích
        if (targetInv == null)
        {
            targetInv = new Nexustock.Modules.Inventory.Entities.Inventory
            {
                Id = Guid.NewGuid(),
                TenantId = task.TenantId,
                ItemId = task.ItemId,
                LocationId = task.TargetLocationId,
                LotNo = task.LotNo,
                QtyOnHand = actualQty,
                QtyReserved = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = operatorName
            };
            _inventoryDb.Inventories.Add(targetInv);
        }
        else
        {
            targetInv.QtyOnHand += actualQty;
            targetInv.UpdatedAt = DateTime.UtcNow;
            targetInv.UpdatedBy = operatorName;
        }

        // 3. Xử lý Under-replenishment (hụt hàng thực tế so với yêu cầu)
        if (actualQty < task.RequestedQty)
        {
            var exception = new OperationalException
            {
                Id = Guid.NewGuid(),
                TenantId = task.TenantId,
                Code = "EX-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),
                Type = "ReplenishmentDiscrepancy",
                Severity = "MEDIUM",
                Status = "Open",
                ReferenceType = "REPLENISHMENT",
                ReferenceId = task.Id,
                LocationId = task.SourceLocationId,
                LotNo = task.LotNo,
                Qty = task.RequestedQty - actualQty,
                ReasonCode = "REPLENISHMENT_DISCREPANCY",
                Note = $"Under-replenishment: Requested {task.RequestedQty}, Actual completed {actualQty} by {operatorName}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = operatorName
            };
            _exceptionsDb.OperationalExceptions.Add(exception);
        }

        // 4. Tạo audit trail dịch chuyển hàng (InventoryMovement)
        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            TenantId = task.TenantId,
            ItemId = task.ItemId,
            LotNo = task.LotNo,
            FromLocationId = task.SourceLocationId,
            ToLocationId = task.TargetLocationId,
            Qty = actualQty,
            Status = "Completed",
            ReasonCode = "REPLENISHMENT",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = operatorName
        };
        _inventoryDb.InventoryMovements.Add(movement);

        // 5. Cập nhật trạng thái task bổ sung
        task.ActualQty = actualQty;
        task.Status = "COMPLETED";
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = operatorName;

        // 6. Đồng bộ chuyển trạng thái MobileTask liên kết
        if (task.MobileTaskId.HasValue)
        {
            var mobileTask = await _inventoryDb.MobileTasks
                .FirstOrDefaultAsync(m => m.Id == task.MobileTaskId.Value);
            if (mobileTask != null)
            {
                mobileTask.Status = "Completed";
                mobileTask.AssignedUser = operatorName;
                mobileTask.UpdatedAt = DateTime.UtcNow;
                mobileTask.UpdatedBy = operatorName;
            }
        }

        await _replenishmentDb.SaveChangesAsync();
        await _inventoryDb.SaveChangesAsync();
        await _exceptionsDb.SaveChangesAsync();
        scope.Complete();

        return task;
    }

    public async Task<ReplenishmentTask> CancelTaskAsync(Guid taskId, string operatorName)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var task = await _replenishmentDb.ReplenishmentTasks
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            throw new ArgumentException("Task không tồn tại");

        if (task.Status == "COMPLETED" || task.Status == "CANCELLED")
            throw new InvalidOperationException("Task không ở trạng thái hợp lệ để hủy");

        // 1. Trả lại lượng giữ (Release reservation) tại kệ Bulk nguồn
        var sourceInv = await _inventoryDb.Inventories
            .FirstOrDefaultAsync(i => i.TenantId == task.TenantId && i.ItemId == task.ItemId && i.LocationId == task.SourceLocationId && i.LotNo == task.LotNo);

        if (sourceInv != null)
        {
            sourceInv.QtyReserved = Math.Max(0, sourceInv.QtyReserved - task.RequestedQty);
            sourceInv.UpdatedAt = DateTime.UtcNow;
            sourceInv.UpdatedBy = operatorName;
        }

        // 2. Chuyển trạng thái task bổ sung thành CANCELLED
        task.Status = "CANCELLED";
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = operatorName;

        // 3. Đồng bộ chuyển MobileTask liên kết thành Cancelled
        if (task.MobileTaskId.HasValue)
        {
            var mobileTask = await _inventoryDb.MobileTasks
                .FirstOrDefaultAsync(m => m.Id == task.MobileTaskId.Value);
            if (mobileTask != null)
            {
                mobileTask.Status = "Cancelled";
                mobileTask.UpdatedAt = DateTime.UtcNow;
                mobileTask.UpdatedBy = operatorName;
            }
        }

        await _replenishmentDb.SaveChangesAsync();
        await _inventoryDb.SaveChangesAsync();
        scope.Complete();

        return task;
    }
}
