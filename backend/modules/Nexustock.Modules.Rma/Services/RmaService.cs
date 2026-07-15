using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Rma.Contexts;
using Nexustock.Modules.Rma.DTOs;
using Nexustock.Modules.Rma.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Inventory.Services;

namespace Nexustock.Modules.Rma.Services;

public class RmaService : IRmaService
{
    private readonly RmaDbContext _db;
    private readonly MasterDataDbContext _masterDb;
    private readonly IInventoryService _inventoryService;
    private readonly ITenantProvider _tenantProvider;

    public RmaService(
        RmaDbContext db, 
        MasterDataDbContext masterDb, 
        IInventoryService inventoryService,
        ITenantProvider tenantProvider)
    {
        _db = db;
        _masterDb = masterDb;
        _inventoryService = inventoryService;
        _tenantProvider = tenantProvider;
    }

    public async Task<RmaDto> CreateRmaAsync(CreateRmaDto dto, string operatorName)
    {
        var tenantId = _tenantProvider.TenantId;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var rmaNo = $"RMA-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            var rma = new RmaRequest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RmaNo = rmaNo,
                CustomerId = dto.CustomerId,
                ReferenceNo = dto.ReferenceNo,
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = operatorName
            };
            await _db.RmaRequests.AddAsync(rma);

            foreach (var itemDto in dto.Items)
            {
                var rmaItem = new RmaItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RmaId = rma.Id,
                    ItemId = itemDto.ItemId,
                    QtyExpected = itemDto.QtyExpected,
                    QtyReceived = 0,
                    SerialNo = itemDto.SerialNo,
                    ReasonCode = itemDto.ReasonCode,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = operatorName
                };
                await _db.RmaItems.AddAsync(rmaItem);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetRmaDetailsAsync(rma.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<RmaDto> ReceiveRmaAsync(Guid rmaId, ReceiveRmaDto dto, string operatorName)
    {
        var tenantId = _tenantProvider.TenantId;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var rma = await _db.RmaRequests
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == rmaId && r.TenantId == tenantId);

            if (rma == null)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu RMA.");

            if (rma.Status != "OPEN" && rma.Status != "RECEIVED")
                throw new InvalidOperationException("Trạng thái RMA không hợp lệ để tiếp nhận hàng.");

            foreach (var recItem in dto.Items)
            {
                var rmaItem = rma.Items.FirstOrDefault(i => i.ItemId == recItem.ItemId && i.SerialNo == recItem.SerialNo);
                if (rmaItem == null)
                {
                    // Hỗ trợ nhận hàng không dự kiến trước nếu cần, hoặc quăng lỗi
                    throw new InvalidOperationException("Sản phẩm không có trong yêu cầu RMA đã lập.");
                }

                rmaItem.QtyReceived += recItem.QtyReceived;
            }

            rma.Status = "RECEIVED";
            rma.UpdatedAt = DateTime.UtcNow;
            rma.UpdatedBy = operatorName;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetRmaDetailsAsync(rma.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<RmaDto> ProcessRmaQcAsync(Guid rmaId, ProcessRmaQcDto dto, string operatorName)
    {
        var tenantId = _tenantProvider.TenantId;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var rma = await _db.RmaRequests
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == rmaId && r.TenantId == tenantId);

            if (rma == null)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu RMA.");

            if (rma.Status != "RECEIVED")
                throw new InvalidOperationException("Chỉ tiếp nhận RMA khi đã ở trạng thái RECEIVED.");

            var traceId = Guid.NewGuid().ToString();

            foreach (var qcResult in dto.Results)
            {
                var rmaItem = rma.Items.FirstOrDefault(i => i.Id == qcResult.RmaItemId);
                if (rmaItem == null)
                    throw new KeyNotFoundException("Không tìm thấy sản phẩm RMA tương ứng để đánh giá QC.");

                // 1. Ghi kết quả QC
                var qc = new RmaQcResult
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RmaItemId = rmaItem.Id,
                    QcStatus = qcResult.QcStatus,
                    Disposition = qcResult.Disposition,
                    Qty = qcResult.Qty,
                    Notes = qcResult.Notes,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = operatorName
                };
                await _db.RmaQcResults.AddAsync(qc);

                // 2. Chuyển đổi trạng thái tồn kho thực tế
                if (qcResult.Disposition == "RESTOCK")
                {
                    if (qcResult.QcStatus != "PASS")
                        throw new InvalidOperationException("Chỉ hàng hóa QC PASS mới được phép RESTOCK vào tồn kho khả dụng.");

                    // Ưu tiên kệ STAGING cho việc hoàn hàng RMA
                    var defaultLocation = await _masterDb.StorageLocations
                        .Where(l => l.TenantId == tenantId && l.IsActive)
                        .OrderByDescending(l => l.Code == "LOC-STG-01") // Ưu tiên code này
                        .ThenByDescending(l => l.MaxCapacity)
                        .FirstOrDefaultAsync();

                    if (defaultLocation == null)
                        throw new InvalidOperationException("Không tìm thấy vị trí kệ kho hợp lệ để hoàn kho.");

                    await _inventoryService.RecordReceiptAsync(
                        tenantId, 
                        rmaItem.ItemId, 
                        $"RMA-{rma.RmaNo}", 
                        defaultLocation.Id, 
                        qcResult.Qty, 
                        operatorName, 
                        traceId);
                }
                // Các disposition khác như QUARANTINE hay SCRAP ghi nhận kết quả QC kết thúc flow, có thể tăng luồng cách ly sau này.
            }

            rma.Status = "QC_COMPLETED";
            rma.UpdatedAt = DateTime.UtcNow;
            rma.UpdatedBy = operatorName;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetRmaDetailsAsync(rma.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<RmaDto> GetRmaDetailsAsync(Guid rmaId)
    {
        var tenantId = _tenantProvider.TenantId;
        var rma = await _db.RmaRequests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == rmaId && r.TenantId == tenantId);

        if (rma == null)
            throw new KeyNotFoundException("Yêu cầu RMA không tồn tại.");

        var prodIds = rma.Items.Select(i => i.ItemId).ToList();
        var products = await _masterDb.Products
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var productCodes = await _masterDb.Products
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Code);

        return new RmaDto
        {
            Id = rma.Id,
            RmaNo = rma.RmaNo,
            CustomerId = rma.CustomerId,
            CustomerName = "Demo Customer",
            ReferenceNo = rma.ReferenceNo,
            Status = rma.Status,
            CreatedAt = rma.CreatedAt,
            CreatedBy = rma.CreatedBy,
            Items = rma.Items.Select(i => new RmaItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                ItemName = products.TryGetValue(i.ItemId, out var name) ? name : "Unknown",
                ItemCode = productCodes.TryGetValue(i.ItemId, out var code) ? code : "Unknown",
                QtyExpected = i.QtyExpected,
                QtyReceived = i.QtyReceived,
                SerialNo = i.SerialNo,
                ReasonCode = i.ReasonCode
            }).ToList()
        };
    }

    public async Task<List<RmaDto>> GetAllRmasAsync()
    {
        var tenantId = _tenantProvider.TenantId;
        var rmas = await _db.RmaRequests
            .Include(r => r.Items)
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var result = new List<RmaDto>();
        foreach (var r in rmas)
        {
            try
            {
                var dto = await GetRmaDetailsAsync(r.Id);
                result.Add(dto);
            }
            catch {}
        }
        return result;
    }
}
