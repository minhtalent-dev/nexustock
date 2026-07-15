using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Wave.Contexts;
using Nexustock.Modules.Wave.Entities;
using Nexustock.Modules.Wave.DTOs;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Serial.Contexts;
using Nexustock.Modules.Serial.Entities;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.Allocation.Services;
using Nexustock.Modules.Allocation.Dtos;

namespace Nexustock.Modules.Wave.Services;

public class WaveService : IWaveService
{
    private readonly WaveDbContext _waveContext;
    private readonly InventoryDbContext _inventoryContext;
    private readonly MasterDataDbContext _masterContext;
    private readonly SerialDbContext _serialContext;
    private readonly IAllocationService _allocationService;
    private readonly ITenantProvider _tenantProvider;

    public WaveService(
        WaveDbContext waveContext,
        InventoryDbContext inventoryContext,
        MasterDataDbContext masterContext,
        SerialDbContext serialContext,
        IAllocationService allocationService,
        ITenantProvider tenantProvider)
    {
        _waveContext = waveContext;
        _inventoryContext = inventoryContext;
        _masterContext = masterContext;
        _serialContext = serialContext;
        _allocationService = allocationService;
        _tenantProvider = tenantProvider;
    }

    public async Task<List<WaveListDto>> GetWavesAsync(Guid tenantId)
    {
        var waves = await _waveContext.PickingWaves
            .Where(w => w.TenantId == tenantId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        var waveIds = waves.Select(w => w.Id).ToList();
        var waveItems = await _waveContext.WaveItems
            .Where(wi => waveIds.Contains(wi.WaveId) && wi.TenantId == tenantId)
            .ToListAsync();

        var itemGroup = waveItems.GroupBy(wi => wi.WaveId).ToDictionary(
            g => g.Key,
            g => new { Count = g.Select(x => x.ItemId).Distinct().Count(), Total = g.Sum(x => x.QtyExpected) }
        );

        return waves.Select(w => new WaveListDto
        {
            Id = w.Id,
            WaveNo = w.WaveNo,
            Status = w.Status,
            CreatedAt = w.CreatedAt,
            CreatedBy = w.CreatedBy,
            ItemCount = itemGroup.TryGetValue(w.Id, out var g) ? g.Count : 0,
            TotalQty = itemGroup.TryGetValue(w.Id, out var g2) ? g2.Total : 0
        }).ToList();
    }

    public async Task<WaveDetailDto> GetWaveDetailsAsync(Guid tenantId, Guid waveId)
    {
        var wave = await _waveContext.PickingWaves
            .FirstOrDefaultAsync(w => w.Id == waveId && w.TenantId == tenantId);
        if (wave == null) throw new KeyNotFoundException("Không tìm thấy đợt Wave");

        var items = await _waveContext.WaveItems
            .Where(wi => wi.WaveId == waveId && wi.TenantId == tenantId)
            .ToListAsync();

        var pickTasks = await _waveContext.WavePickTasks
            .Where(wt => wt.WaveId == waveId && wt.TenantId == tenantId)
            .ToListAsync();

        var itemIds = items.Select(i => i.ItemId).Concat(pickTasks.Select(t => t.ItemId)).Distinct().ToList();
        var products = await _masterContext.Products
            .Where(p => itemIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Code });

        var uomIds = await _masterContext.Products
            .Where(p => itemIds.Contains(p.Id))
            .Select(p => p.BaseUomId)
            .Distinct()
            .ToListAsync();

        var uoms = await _masterContext.Uoms
            .Where(u => uomIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var shipmentIds = items.Select(i => i.ShipmentId).Distinct().ToList();
        var shipments = await _inventoryContext.Shipments
            .Where(s => shipmentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.ShipmentNo);

        var locationIds = pickTasks.Select(t => t.FromLocationId).Distinct().ToList();
        var locations = await _masterContext.StorageLocations
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code);

        // Deterministic Slot Map
        var sortedShipmentIds = shipmentIds.OrderBy(id => id).ToList();
        var slotMap = sortedShipmentIds.Select((id, idx) => new { Id = id, Slot = idx + 1 }).ToDictionary(x => x.Id, x => x.Slot);

        var itemDtos = items.Select(i => new WaveItemDetailDto
        {
            Id = i.Id,
            ShipmentId = i.ShipmentId,
            ShipmentNo = shipments.TryGetValue(i.ShipmentId, out var no) ? no : "Unknown",
            ShipmentItemId = i.ShipmentItemId,
            ItemId = i.ItemId,
            ItemName = products.TryGetValue(i.ItemId, out var p) ? p.Name : "Unknown",
            ItemCode = products.TryGetValue(i.ItemId, out var p2) ? p2.Code : "Unknown",
            UomName = products.TryGetValue(i.ItemId, out var p3) && _masterContext.Products.FirstOrDefault(pr => pr.Id == i.ItemId) is var prObj && prObj != null && uoms.TryGetValue(prObj.BaseUomId, out var u) ? u : "Unknown",
            QtyExpected = i.QtyExpected,
            QtyAllocated = i.QtyAllocated,
            QtyPicked = i.QtyPicked,
            QtySorted = i.QtySorted,
            RecommendedSlotNumber = slotMap.TryGetValue(i.ShipmentId, out var slot) ? slot : null
        }).ToList();

        var taskDtos = pickTasks.Select(t => new WavePickTaskDto
        {
            Id = t.Id,
            ItemId = t.ItemId,
            ItemName = products.TryGetValue(t.ItemId, out var prod) ? prod.Name : "Unknown",
            ItemCode = products.TryGetValue(t.ItemId, out var prod2) ? prod2.Code : "Unknown",
            FromLocationId = t.FromLocationId,
            LocationCode = locations.TryGetValue(t.FromLocationId, out var loc) ? loc : "Unknown",
            QtyToPick = t.QtyToPick,
            QtyPicked = t.QtyPicked,
            Status = t.Status
        }).ToList();

        return new WaveDetailDto
        {
            Id = wave.Id,
            WaveNo = wave.WaveNo,
            Status = wave.Status,
            CreatedAt = wave.CreatedAt,
            CreatedBy = wave.CreatedBy,
            Items = itemDtos,
            PickTasks = taskDtos
        };
    }

    public async Task<Guid> CreateWaveAsync(Guid tenantId, string username, CreateWaveDto dto)
    {
        if (dto.ShipmentIds == null || !dto.ShipmentIds.Any())
            throw new ArgumentException("Danh sách ShipmentIds không được trống.");

        var shipments = await _inventoryContext.Shipments
            .Where(s => dto.ShipmentIds.Contains(s.Id) && s.TenantId == tenantId)
            .ToListAsync();

        if (shipments.Count != dto.ShipmentIds.Count)
            throw new ArgumentException("Một hoặc nhiều đơn xuất kho không hợp lệ.");

        if (shipments.Any(s => s.Status != "Open"))
            throw new InvalidOperationException("Chỉ cho phép gộp các đơn xuất kho ở trạng thái Open.");

        using var transaction = await _waveContext.Database.BeginTransactionAsync();
        try
        {
            var waveId = Guid.NewGuid();
            var waveNo = $"WAVE-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";

            var wave = new PickingWave
            {
                Id = waveId,
                TenantId = tenantId,
                WaveNo = waveNo,
                Status = "DRAFT",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            await _waveContext.PickingWaves.AddAsync(wave);

            var shipmentItems = await _inventoryContext.ShipmentItems
                .Where(si => dto.ShipmentIds.Contains(si.ShipmentId) && si.TenantId == tenantId)
                .ToListAsync();

            foreach (var si in shipmentItems)
            {
                var waveItem = new WaveItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WaveId = waveId,
                    ShipmentId = si.ShipmentId,
                    ShipmentItemId = si.Id,
                    ItemId = si.ItemId,
                    QtyExpected = si.RequestedQty,
                    QtyAllocated = 0,
                    QtyPicked = 0,
                    QtySorted = 0
                };
                await _waveContext.WaveItems.AddAsync(waveItem);
            }

            // Đổi trạng thái các Shipment thành Waving
            foreach (var s in shipments)
            {
                s.Status = "Waving";
                s.UpdatedAt = DateTime.UtcNow;
                s.UpdatedBy = username;
            }

            await _waveContext.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return waveId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task ReleaseWaveAsync(Guid tenantId, string username, Guid waveId)
    {
        var wave = await _waveContext.PickingWaves
            .FirstOrDefaultAsync(w => w.Id == waveId && w.TenantId == tenantId);
        if (wave == null) throw new KeyNotFoundException("Không tìm thấy đợt Wave");

        if (wave.Status != "DRAFT")
            throw new InvalidOperationException("Chỉ cho phép Release Wave ở trạng thái DRAFT");

        var waveItems = await _waveContext.WaveItems
            .Where(wi => wi.WaveId == waveId && wi.TenantId == tenantId)
            .ToListAsync();

        var shipmentIds = waveItems.Select(wi => wi.ShipmentId).Distinct().ToList();

        using var transaction = await _waveContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Chạy Allocation cho từng Shipment
            foreach (var shipmentId in shipmentIds)
            {
                await _allocationService.AllocateAsync(tenantId, new ReserveRequestDto
                {
                    ShipmentId = shipmentId,
                    Strategy = "FIFO",
                    AllowPartial = true
                }, username);
            }

            // 2. Đọc lại ShipmentItem trong InventoryDbContext để cập nhật WaveItem
            var shipmentItems = await _inventoryContext.ShipmentItems
                .Where(si => shipmentIds.Contains(si.ShipmentId) && si.TenantId == tenantId)
                .ToListAsync();

            foreach (var wi in waveItems)
            {
                var siObj = shipmentItems.FirstOrDefault(si => si.Id == wi.ShipmentItemId);
                if (siObj != null)
                {
                    wi.QtyAllocated = siObj.AllocatedQty;
                    // ERP Reconciliation: Giữ nguyên qty_expected
                }
            }

            // Sinh PickTask lẻ cho từng AllocationReservation nếu chưa có
            var existingPicks = await _inventoryContext.PickTasks
                .Where(pt => shipmentIds.Contains(pt.ShipmentId) && pt.TenantId == tenantId)
                .ToListAsync();

            if (!existingPicks.Any())
            {
                var shipmentLineIds = shipmentItems.Select(l => l.Id).ToList();
                var activeReservations = await _inventoryContext.AllocationReservations
                    .Where(r => shipmentLineIds.Contains(r.ShipmentLineId) && r.TenantId == tenantId && r.Status == "ACTIVE")
                    .ToListAsync();

                var balanceIds = activeReservations.Select(r => r.InventoryBalanceId).Distinct().ToList();
                var balances = await _inventoryContext.Inventories
                    .Where(b => balanceIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id, b => b);

                foreach (var res in activeReservations)
                {
                    var lineObj = shipmentItems.First(l => l.Id == res.ShipmentLineId);
                    if (balances.TryGetValue(res.InventoryBalanceId, out var bal))
                    {
                        var pickTask = new PickTask
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            ShipmentId = lineObj.ShipmentId,
                            ItemId = lineObj.ItemId,
                            LotNo = bal.LotNo,
                            FromLocationId = bal.LocationId,
                            Qty = res.Qty,
                            PickedQty = 0,
                            Status = "Pending",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = username
                        };
                        await _inventoryContext.PickTasks.AddAsync(pickTask);
                    }
                }
                await _inventoryContext.SaveChangesAsync();
            }

            // 3. Lấy các PickTask được sinh ra cho các Shipments này trong InventoryDbContext
            var pickTasks = await _inventoryContext.PickTasks
                .Where(pt => shipmentIds.Contains(pt.ShipmentId) && pt.TenantId == tenantId && pt.Status == "Pending")
                .ToListAsync();

            // Chuyển trạng thái các PickTask lẻ thành Waved để ẩn khỏi RF Mobile
            foreach (var pt in pickTasks)
            {
                pt.Status = "Waved";
                pt.UpdatedAt = DateTime.UtcNow;
                pt.UpdatedBy = username;
            }

            // 4. Nhóm gộp sinh WavePickTask tổng hợp
            var groupedTasks = pickTasks
                .GroupBy(pt => new { pt.ItemId, pt.FromLocationId })
                .ToList();

            foreach (var group in groupedTasks)
            {
                var wavePickTask = new WavePickTask
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WaveId = waveId,
                    ItemId = group.Key.ItemId,
                    FromLocationId = group.Key.FromLocationId,
                    QtyToPick = group.Sum(pt => pt.Qty),
                    QtyPicked = 0,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                await _waveContext.WavePickTasks.AddAsync(wavePickTask);
            }

            wave.Status = "RELEASED";
            wave.UpdatedAt = DateTime.UtcNow;
            wave.UpdatedBy = username;

            await _waveContext.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task CompletePickTaskAsync(Guid tenantId, string username, CompleteWavePickDto dto)
    {
        var task = await _waveContext.WavePickTasks
            .FirstOrDefaultAsync(t => t.Id == dto.TaskId && t.TenantId == tenantId);
        if (task == null) throw new KeyNotFoundException("Không tìm thấy nhiệm vụ lấy hàng tổng hợp.");

        if (task.Status != "PENDING")
            throw new InvalidOperationException("Nhiệm vụ lấy hàng đã được xử lý trước đó.");

        if (dto.PickedQty <= 0 || dto.PickedQty > task.QtyToPick)
            throw new ArgumentException("Số lượng lấy hàng không hợp lệ.");

        var product = await _masterContext.Products
            .FirstOrDefaultAsync(p => p.Id == task.ItemId && p.TenantId == tenantId);
        if (product != null && product.IsSerialTracked)
        {
            if (dto.SerialNos == null || dto.SerialNos.Count != (int)dto.PickedQty)
            {
                throw new ArgumentException("Mã Serial không hợp lệ hoặc thiếu mã Serial.");
            }
        }

        using var transaction = await _waveContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Trừ tồn kho tại vị trí kệ nguồn và dịch chuyển sang LOC-SORT-01 (Khu vực phân chia)
            // Lấy các inventories tương ứng tại FromLocationId để dịch chuyển
            var inventories = await _inventoryContext.Inventories
                .Where(i => i.ItemId == task.ItemId && i.LocationId == task.FromLocationId && i.TenantId == tenantId)
                .ToListAsync();

            // Tìm vị trí LOC-SORT-01 trong MasterData
            var sortLocation = await _masterContext.StorageLocations
                .FirstOrDefaultAsync(l => l.Code == "LOC-SORT-01" && l.TenantId == tenantId);
            if (sortLocation == null)
            {
                throw new InvalidOperationException("Vị trí tạm thời 'LOC-SORT-01' chưa được cấu hình trong Master Data.");
            }

            decimal remainingToDeduct = dto.PickedQty;
            foreach (var inv in inventories)
            {
                if (remainingToDeduct <= 0) break;
                
                decimal deductQty = Math.Min(inv.QtyOnHand, remainingToDeduct);
                inv.QtyOnHand -= deductQty;
                inv.QtyReserved = Math.Max(0, inv.QtyReserved - deductQty); // Giải phóng reserve
                inv.UpdatedAt = DateTime.UtcNow;
                inv.UpdatedBy = username;

                if (inv.QtyOnHand <= 0 && inv.QtyReserved <= 0)
                {
                    _inventoryContext.Inventories.Remove(inv);
                }

                // Cộng vào vị trí LOC-SORT-01
                var sortInv = await _inventoryContext.Inventories
                    .FirstOrDefaultAsync(i => i.ItemId == task.ItemId && i.LocationId == sortLocation.Id && i.LotNo == inv.LotNo && i.TenantId == tenantId);
                
                if (sortInv == null)
                {
                    sortInv = new Inventory.Entities.Inventory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ItemId = task.ItemId,
                        LocationId = sortLocation.Id,
                        LotNo = inv.LotNo,
                        QtyOnHand = deductQty,
                        QtyReserved = 0,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username
                    };
                    await _inventoryContext.Inventories.AddAsync(sortInv);
                }
                else
                {
                    sortInv.QtyOnHand += deductQty;
                    sortInv.UpdatedAt = DateTime.UtcNow;
                    sortInv.UpdatedBy = username;
                }

                remainingToDeduct -= deductQty;
            }

            // 2. Cập nhật task
            task.QtyPicked = dto.PickedQty;
            task.Status = "COMPLETED";
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = username;

            // 3. Phân bổ số lượng pick cho từng WaveItem theo FIFO
            var waveItems = await _waveContext.WaveItems
                .Where(wi => wi.WaveId == task.WaveId && wi.ItemId == task.ItemId && wi.TenantId == tenantId)
                .ToListAsync();

            decimal remainingPickQty = dto.PickedQty;
            foreach (var wi in waveItems)
            {
                if (remainingPickQty <= 0) break;
                
                decimal allocToItem = Math.Min(wi.QtyAllocated - wi.QtyPicked, remainingPickQty);
                if (allocToItem > 0)
                {
                    wi.QtyPicked += allocToItem;
                    remainingPickQty -= allocToItem;
                }
            }

            // 4. Nếu có Serial, cập nhật trạng thái các Serial đã quét thành SORTING và lưu vết
            if (product != null && product.IsSerialTracked && dto.SerialNos != null)
            {
                var serials = await _serialContext.SerialNumbers
                    .Where(s => dto.SerialNos.Contains(s.SerialNo) && s.ItemId == task.ItemId && s.TenantId == tenantId)
                    .ToListAsync();

                foreach (var s in serials)
                {
                    s.Status = "SORTING";
                    s.LocationId = sortLocation.Id;
                    s.UpdatedAt = DateTime.UtcNow;
                    s.UpdatedBy = username;

                    var serialEvent = new SerialEvent
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        SerialId = s.Id,
                        EventType = "WAVE_PICK",
                        FromLocationId = task.FromLocationId,
                        ToLocationId = sortLocation.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username
                    };
                    await _serialContext.SerialEvents.AddAsync(serialEvent);
                }
            }

            await _waveContext.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await _serialContext.SaveChangesAsync();
            await transaction.CommitAsync();

            // 5. Kiểm tra nếu tất cả các Task của Wave đã xong thì tự động chuyển trạng thái Wave thành SORTING
            var allTasks = await _waveContext.WavePickTasks
                .Where(wt => wt.WaveId == task.WaveId && wt.TenantId == tenantId)
                .ToListAsync();

            if (allTasks.All(t => t.Status == "COMPLETED"))
            {
                var wave = await _waveContext.PickingWaves.FirstOrDefaultAsync(w => w.Id == task.WaveId && w.TenantId == tenantId);
                if (wave != null)
                {
                    wave.Status = "SORTING";
                    wave.UpdatedAt = DateTime.UtcNow;
                    wave.UpdatedBy = username;
                    await _waveContext.SaveChangesAsync();
                }
            }
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SortResponseDto> SortItemAsync(Guid tenantId, Guid waveId, SortRequestDto dto)
    {
        var wave = await _waveContext.PickingWaves
            .FirstOrDefaultAsync(w => w.Id == waveId && w.TenantId == tenantId);
        if (wave == null) throw new KeyNotFoundException("Không tìm thấy đợt Wave");

        if (wave.Status != "SORTING")
            throw new InvalidOperationException("Đợt Wave không ở trạng thái phân chia (SORTING)");

        Guid itemId;
        string? scannedSerial = null;

        // 1. Kiểm tra xem quét mã Serial hay barcode sản phẩm
        var serial = await _serialContext.SerialNumbers
            .FirstOrDefaultAsync(s => s.SerialNo == dto.BarcodeOrSerial && s.TenantId == tenantId);

        if (serial != null)
        {
            itemId = serial.ItemId;
            scannedSerial = serial.SerialNo;
        }
        else
        {
            var product = await _masterContext.Products
                .FirstOrDefaultAsync(p => p.Code == dto.BarcodeOrSerial && p.TenantId == tenantId);
            if (product == null)
            {
                throw new ArgumentException("Mã vạch hoặc mã Serial không hợp lệ.");
            }
            itemId = product.Id;
        }

        // 2. Deterministic Slot Assignment map
        var waveItems = await _waveContext.WaveItems
            .Where(wi => wi.WaveId == waveId && wi.TenantId == tenantId)
            .ToListAsync();

        var shipmentIds = waveItems.Select(i => i.ShipmentId).Distinct().ToList();
        var sortedShipmentIds = shipmentIds.OrderBy(id => id).ToList();
        var slotMap = sortedShipmentIds.Select((id, idx) => new { Id = id, Slot = idx + 1 }).ToDictionary(x => x.Id, x => x.Slot);

        // 3. Tìm WaveItem đầu tiên cần phân loại sản phẩm này (theo Slot Number tăng dần)
        var targetItem = waveItems
            .Where(wi => wi.ItemId == itemId && wi.QtySorted < wi.QtyPicked)
            .OrderBy(wi => slotMap.TryGetValue(wi.ShipmentId, out var slot) ? slot : 9999)
            .FirstOrDefault();

        if (targetItem == null)
        {
            throw new InvalidOperationException("Sản phẩm quét không nằm trong danh sách cần phân chia hoặc đã sort đủ.");
        }

        using var transaction = await _waveContext.Database.BeginTransactionAsync();
        try
        {
            targetItem.QtySorted += 1;

            if (scannedSerial != null && serial != null)
            {
                // Cập nhật Serial sang SORTED
                serial.Status = "SORTED";
                serial.UpdatedAt = DateTime.UtcNow;
                serial.UpdatedBy = "System";

                var serialEvent = new SerialEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SerialId = serial.Id,
                    EventType = "WAVE_SORT",
                    FromLocationId = serial.LocationId,
                    ToLocationId = Guid.Empty, // Vị trí Put-Wall ảo
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };
                await _serialContext.SerialEvents.AddAsync(serialEvent);
            }

            await _waveContext.SaveChangesAsync();
            await _serialContext.SaveChangesAsync();
            await transaction.CommitAsync();

            var shipment = await _inventoryContext.Shipments
                .FirstAsync(s => s.Id == targetItem.ShipmentId && s.TenantId == tenantId);

            var productInfo = await _masterContext.Products.FirstAsync(p => p.Id == itemId);

            // Kiểm tra xem Shipment này trong Wave đã sort đủ chưa
            var shipmentItemsInWave = waveItems.Where(wi => wi.ShipmentId == targetItem.ShipmentId).ToList();
            bool isSlotComplete = shipmentItemsInWave.All(wi => wi.QtySorted == wi.QtyPicked);

            return new SortResponseDto
            {
                ShipmentId = targetItem.ShipmentId,
                ShipmentNo = shipment.ShipmentNo,
                RecommendedSlotNumber = slotMap[targetItem.ShipmentId],
                ItemName = productInfo.Name,
                ItemCode = productInfo.Code,
                QtySorted = targetItem.QtySorted,
                QtyExpected = targetItem.QtyPicked, // Hiển thị số lượng thực pick mang về làm mốc hoàn thành slot
                IsSlotComplete = isSlotComplete
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task CompleteWaveAsync(Guid tenantId, string username, Guid waveId)
    {
        var wave = await _waveContext.PickingWaves
            .FirstOrDefaultAsync(w => w.Id == waveId && w.TenantId == tenantId);
        if (wave == null) throw new KeyNotFoundException("Không tìm thấy đợt Wave");

        if (wave.Status != "SORTING")
            throw new InvalidOperationException("Đợt Wave không ở trạng thái phân chia (SORTING)");

        var waveItems = await _waveContext.WaveItems
            .Where(wi => wi.WaveId == waveId && wi.TenantId == tenantId)
            .ToListAsync();

        using var transaction = await _waveContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Cập nhật trạng thái Shipment gốc thành Picking (đã pick xong, sẵn sàng đóng gói)
            // Cập nhật PickedQty trên ShipmentItem trong InventoryDbContext bằng QtySorted thực tế
            var shipmentIds = waveItems.Select(wi => wi.ShipmentId).Distinct().ToList();
            var shipments = await _inventoryContext.Shipments
                .Where(s => shipmentIds.Contains(s.Id) && s.TenantId == tenantId)
                .ToListAsync();

            var shipmentItems = await _inventoryContext.ShipmentItems
                .Where(si => shipmentIds.Contains(si.ShipmentId) && si.TenantId == tenantId)
                .ToListAsync();

            foreach (var s in shipments)
            {
                s.Status = "Picking"; // Sẵn sàng Đóng gói (Packing)
                s.UpdatedAt = DateTime.UtcNow;
                s.UpdatedBy = username;
            }

            foreach (var si in shipmentItems)
            {
                var wi = waveItems.FirstOrDefault(w => w.ShipmentItemId == si.Id);
                if (wi != null)
                {
                    si.PickedQty = wi.QtySorted;
                    si.Status = "Picked";
                }
            }

            // 2. Chuyển trạng thái Wave sang COMPLETED
            wave.Status = "COMPLETED";
            wave.UpdatedAt = DateTime.UtcNow;
            wave.UpdatedBy = username;

            await _waveContext.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
