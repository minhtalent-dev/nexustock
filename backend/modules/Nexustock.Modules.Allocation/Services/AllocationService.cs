using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Allocation.Dtos;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.Inbound.Contexts;

namespace Nexustock.Modules.Allocation.Services;

public interface IAllocationService
{
    Task<ReserveResponseDto> AllocateAsync(Guid tenantId, ReserveRequestDto dto, string username);
    Task<bool> ReleaseAsync(Guid tenantId, Guid shipmentId, string username);
    Task<ReserveResponseDto> ReallocateAsync(Guid tenantId, Guid shipmentId, string username);
    Task<AvailabilityResponseDto> GetAvailabilityAsync(Guid tenantId, Guid itemId);
}

public class AllocationService : IAllocationService
{
    private readonly InventoryDbContext _inventoryContext;
    private readonly InboundDbContext _inboundContext;
    private readonly ILogger<AllocationService> _logger;

    public AllocationService(
        InventoryDbContext inventoryContext,
        InboundDbContext inboundContext,
        ILogger<AllocationService> logger)
    {
        _inventoryContext = inventoryContext;
        _inboundContext = inboundContext;
        _logger = logger;
    }

    public async Task<ReserveResponseDto> AllocateAsync(Guid tenantId, ReserveRequestDto dto, string username)
    {
        const int maxRetries = 3;
        const int delayMs = 50;
        Exception? lastException = null;

        for (int retry = 0; retry < maxRetries; retry++)
        {
            using var transaction = await _inventoryContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                // 1. Fetch Shipment
                var shipment = await _inventoryContext.Shipments
                    .FirstOrDefaultAsync(s => s.Id == dto.ShipmentId && s.TenantId == tenantId);
                if (shipment == null)
                {
                    throw new KeyNotFoundException($"Không tìm thấy đơn xuất kho với ID '{dto.ShipmentId}'");
                }

                // 2. Fetch Shipment items (Lines) that are not fully allocated
                var shipmentLines = await _inventoryContext.ShipmentItems
                    .Where(si => si.ShipmentId == dto.ShipmentId && si.TenantId == tenantId && si.Status != "Allocated")
                    .ToListAsync();

                if (!shipmentLines.Any())
                {
                    return new ReserveResponseDto
                    {
                        Success = true,
                        ShipmentId = dto.ShipmentId,
                        Status = shipment.Status,
                        Message = "Đơn xuất kho đã được phân bổ đầy đủ trước đó."
                    };
                }

                // CHỐNG DEADLOCK: Sắp xếp các ItemId tăng dần để đảm bảo thứ tự Lock nhất quán
                var sortedLines = shipmentLines.OrderBy(l => l.ItemId).ToList();
                var allocatedLinesDto = new List<AllocatedLineDto>();

                foreach (var line in sortedLines)
                {
                    decimal remainingQty = line.RequestedQty - line.AllocatedQty;
                    if (remainingQty <= 0) continue;

                    // A. Query candidate inventories for this ItemId
                    // Lọc các vị trí không bị khóa
                    var lockedLocIds = await _inventoryContext.LocationLocks
                        .Where(l => l.TenantId == tenantId)
                        .Select(l => l.LocationId)
                        .ToListAsync();

                    var candidateBalances = await _inventoryContext.Inventories
                        .Where(i => i.TenantId == tenantId && i.ItemId == line.ItemId && !lockedLocIds.Contains(i.LocationId) && (i.QtyOnHand - i.QtyReserved) > 0)
                        .ToListAsync();

                    if (!candidateBalances.Any())
                    {
                        if (!dto.AllowPartial)
                        {
                            throw new InvalidOperationException($"Không đủ tồn kho khả dụng cho vật tư ID '{line.ItemId}'");
                        }
                        line.Status = "PartiallyAllocated";
                        continue;
                    }

                    // CHỐNG DEADLOCK NÂNG CAO: Sắp xếp các dòng tồn kho theo ID GUID tăng dần trước khi Lock vật lý
                    var sortedBalancesToLock = candidateBalances.OrderBy(i => i.Id).ToList();
                    var balanceIds = sortedBalancesToLock.Select(i => i.Id).ToArray();

                    // Thực hiện Pessimistic Lock (SELECT FOR UPDATE) trên các dòng inventories đã được sắp xếp
                    // Sử dụng PostgreSQL ANY
                    var lockedBalances = await _inventoryContext.Inventories
                        .FromSqlRaw("SELECT * FROM inventories WHERE id = ANY({0}) FOR UPDATE", balanceIds)
                        .ToListAsync();

                    // B. Lấy thông tin hạn dùng và ngày sản xuất của các lô hàng từ module Inbound
                    var lotNos = lockedBalances.Select(b => b.LotNo).Distinct().ToList();
                    var inboundLots = await _inboundContext.Lots
                        .Where(l => l.TenantId == tenantId && l.ItemId == line.ItemId && lotNos.Contains(l.LotNo))
                        .ToListAsync();

                    // Lọc tiếp chỉ giữ lại những lô đã được duyệt QC (Release)
                    var releasedLotNos = inboundLots
                        .Where(l => l.QcStatus == Nexustock.Modules.Inbound.Entities.LotQcStatus.Release)
                        .Select(l => l.LotNo)
                        .ToList();

                    var activeBalances = lockedBalances
                        .Where(b => releasedLotNos.Contains(b.LotNo))
                        .ToList();

                    // C. Sắp xếp dòng tồn kho theo chiến lược FEFO/FIFO và Tie-break trong bộ nhớ
                    var lotMetadata = inboundLots.ToDictionary(l => l.LotNo, l => l);
                    
                    IOrderedEnumerable<Nexustock.Modules.Inventory.Entities.Inventory> sortedBalances;
                    if (dto.Strategy.Equals("FIFO", StringComparison.OrdinalIgnoreCase))
                    {
                        sortedBalances = activeBalances
                            .OrderBy(b => lotMetadata.TryGetValue(b.LotNo, out var l) ? l.ProductionDate ?? DateTime.MaxValue : DateTime.MaxValue)
                            .ThenBy(b => b.CreatedAt)
                            .ThenBy(b => b.Id);
                    }
                    else // FEFO
                    {
                        sortedBalances = activeBalances
                            .OrderBy(b => lotMetadata.TryGetValue(b.LotNo, out var l) ? l.ExpiryDate ?? DateTime.MaxValue : DateTime.MaxValue)
                            .ThenBy(b => lotMetadata.TryGetValue(b.LotNo, out var l) ? l.ProductionDate ?? DateTime.MaxValue : DateTime.MaxValue)
                            .ThenBy(b => b.CreatedAt)
                            .ThenBy(b => b.Id);
                    }

                    decimal totalAllocatedQty = 0;
                    var reservationsDetail = new List<ReservationDetailDto>();

                    foreach (var balance in sortedBalances)
                    {
                        decimal availableQty = balance.QtyOnHand - balance.QtyReserved;
                        if (availableQty <= 0) continue;

                        decimal allocatedQty = Math.Min(remainingQty, availableQty);

                        // Cập nhật số giữ hàng trên Inventory
                        balance.QtyReserved += allocatedQty;
                        balance.UpdatedAt = DateTime.UtcNow;
                        balance.UpdatedBy = username;
                        balance.RowVersion += 1;

                        // Tạo bản ghi Allocation Reservation
                        var reservation = new AllocationReservation
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            WarehouseId = Guid.Empty, // Hoặc lấy từ location metadata nếu có
                            ShipmentLineId = line.Id,
                            InventoryBalanceId = balance.Id,
                            Qty = allocatedQty,
                            Status = "ACTIVE",
                            ExpiresAt = DateTime.UtcNow.AddMinutes(dto.ReservationTtlMinutes),
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = username
                        };
                        _inventoryContext.AllocationReservations.Add(reservation);

                        // P36: tạo PickTask cùng TX khi GeneratePicks yêu cầu (Wave giữ CreatePickTasks=false)
                        if (dto.CreatePickTasks)
                        {
                            _inventoryContext.PickTasks.Add(new PickTask
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                ShipmentId = dto.ShipmentId,
                                ItemId = line.ItemId,
                                LotNo = balance.LotNo,
                                FromLocationId = balance.LocationId,
                                Qty = allocatedQty,
                                PickedQty = 0,
                                Status = "Pending",
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = username
                            });
                        }

                        totalAllocatedQty += allocatedQty;
                        remainingQty -= allocatedQty;

                        // Tìm location code phục vụ DTO
                        // Để đơn giản và nhanh, ta có thể lấy location code từ cached master data hoặc trả về placeholder.
                        // (Thường API sẽ lookup, ở đây ta trả về ID để client tự map hoặc nạp sau)
                        reservationsDetail.Add(new ReservationDetailDto
                        {
                            ReservationId = reservation.Id,
                            LocationCode = balance.LocationId.ToString(), // Trả về ID tạm thời
                            LotNo = balance.LotNo,
                            Qty = allocatedQty
                        });

                        if (remainingQty <= 0) break;
                    }

                    line.AllocatedQty += totalAllocatedQty;
                    line.RowVersion += 1;

                    if (remainingQty > 0)
                    {
                        if (!dto.AllowPartial)
                        {
                            throw new InvalidOperationException($"Không đủ tồn kho khả dụng để phân bổ đầy đủ cho vật tư ID '{line.ItemId}'");
                        }
                        line.Status = "PartiallyAllocated";
                    }
                    else
                    {
                        line.Status = "Allocated";
                    }

                    allocatedLinesDto.Add(new AllocatedLineDto
                    {
                        ShipmentLineId = line.Id,
                        ItemId = line.ItemId,
                        RequestedQty = line.RequestedQty,
                        AllocatedQty = line.AllocatedQty,
                        Reservations = reservationsDetail
                    });
                }

                // 3. Cập nhật trạng thái Shipment tổng thể
                var allLines = await _inventoryContext.ShipmentItems
                    .Where(si => si.ShipmentId == dto.ShipmentId && si.TenantId == tenantId)
                    .ToListAsync();

                if (allLines.All(l => l.Status == "Allocated"))
                {
                    shipment.Status = "Allocated";
                }
                else if (allLines.Any(l => l.Status == "Allocated" || l.Status == "PartiallyAllocated"))
                {
                    shipment.Status = "PartiallyAllocated";
                }
                else
                {
                    shipment.Status = "Unallocated";
                }

                shipment.UpdatedAt = DateTime.UtcNow;
                shipment.UpdatedBy = username;

                await _inventoryContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return new ReserveResponseDto
                {
                    Success = true,
                    ShipmentId = dto.ShipmentId,
                    Status = shipment.Status,
                    AllocatedLines = allocatedLinesDto,
                    Message = "Phân bổ tồn kho thành công."
                };
            }
            catch (Npgsql.NpgsqlException ex) when (ex.SqlState == "40P01") // Deadlock PostgreSQL
            {
                await transaction.RollbackAsync();
                lastException = ex;
                _logger.LogWarning("Phát hiện Deadlock (40P01), đang retry lần thứ {Retry}...", retry + 1);
                await Task.Delay(delayMs * (retry + 1));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        throw new TimeoutException("Gặp lỗi tranh chấp khóa phân bổ kéo dài. Vui lòng thử lại sau.", lastException);
    }

    public async Task<bool> ReleaseAsync(Guid tenantId, Guid shipmentId, string username)
    {
        using var transaction = await _inventoryContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var shipment = await _inventoryContext.Shipments
                .FirstOrDefaultAsync(s => s.Id == shipmentId && s.TenantId == tenantId);
            if (shipment == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn xuất kho với ID '{shipmentId}'");
            }

            var lines = await _inventoryContext.ShipmentItems
                .Where(si => si.ShipmentId == shipmentId && si.TenantId == tenantId)
                .ToListAsync();

            var lineIds = lines.Select(l => l.Id).ToList();
            var reservations = await _inventoryContext.AllocationReservations
                .Where(r => r.TenantId == tenantId && lineIds.Contains(r.ShipmentLineId) && r.Status == "ACTIVE")
                .ToListAsync();

            if (!reservations.Any())
            {
                return true;
            }

            var balanceIds = reservations.Select(r => r.InventoryBalanceId).Distinct().ToList();
            var balances = await _inventoryContext.Inventories
                .Where(i => balanceIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i);

            foreach (var res in reservations)
            {
                res.Status = "RELEASED";
                res.UpdatedAt = DateTime.UtcNow;
                res.UpdatedBy = username;

                if (balances.TryGetValue(res.InventoryBalanceId, out var balance))
                {
                    balance.QtyReserved = Math.Max(0, balance.QtyReserved - res.Qty);
                    balance.UpdatedAt = DateTime.UtcNow;
                    balance.UpdatedBy = username;
                    balance.RowVersion += 1;
                }
            }

            foreach (var line in lines)
            {
                line.AllocatedQty = 0;
                line.Status = "Unallocated";
                line.RowVersion += 1;
            }

            shipment.Status = "Unallocated";
            shipment.UpdatedAt = DateTime.UtcNow;
            shipment.UpdatedBy = username;

            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ReserveResponseDto> ReallocateAsync(Guid tenantId, Guid shipmentId, string username)
    {
        // Nhả phân bổ trước
        await ReleaseAsync(tenantId, shipmentId, username);

        // Chạy lại phân bổ
        return await AllocateAsync(tenantId, new ReserveRequestDto
        {
            ShipmentId = shipmentId,
            Strategy = "FEFO",
            AllowPartial = true,
            ReservationTtlMinutes = 1440
        }, username);
    }

    public async Task<AvailabilityResponseDto> GetAvailabilityAsync(Guid tenantId, Guid itemId)
    {
        var inventories = await _inventoryContext.Inventories
            .Where(i => i.TenantId == tenantId && i.ItemId == itemId)
            .ToListAsync();

        decimal qtyOnHand = inventories.Sum(i => i.QtyOnHand);
        decimal qtyReserved = inventories.Sum(i => i.QtyReserved);
        decimal qtyAvailable = inventories.Sum(i => i.QtyAvailable);

        return new AvailabilityResponseDto
        {
            ItemId = itemId,
            ItemCode = string.Empty, // lookup hoặc trả về trống để client match
            QtyOnHand = qtyOnHand,
            QtyReserved = qtyReserved,
            QtyAvailable = qtyAvailable
        };
    }
}
