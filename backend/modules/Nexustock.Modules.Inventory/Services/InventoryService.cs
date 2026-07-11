using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.Modules.Inventory.Services;

public class InventoryService : IInventoryService
{
    private readonly InventoryDbContext _context;
    private readonly MasterDataDbContext _masterContext;

    public InventoryService(InventoryDbContext context, MasterDataDbContext masterContext)
    {
        _context = context;
        _masterContext = masterContext;
    }

    public async Task RecordReceiptAsync(
        Guid tenantId,
        Guid itemId,
        string lotNo,
        Guid toLocationId,
        decimal qty,
        string username,
        string traceId)
    {
        // 1. Check Capacity Guard
        var location = await _masterContext.StorageLocations
            .IgnoreQueryFilters() // StorageLocation might have different tenant filter or none
            .FirstOrDefaultAsync(l => l.Id == toLocationId && l.TenantId == tenantId);

        if (location != null)
        {
            var currentQty = await _context.Inventories
                .Where(i => i.LocationId == toLocationId && i.TenantId == tenantId)
                .SumAsync(i => i.QtyOnHand);

            if (currentQty + qty > location.MaxCapacity)
            {
                throw new InvalidOperationException("LOCATION_OVER_CAPACITY");
            }
        }

        // 2. Try to fetch existing inventory balance
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && 
                                      i.ItemId == itemId && 
                                      i.LotNo == lotNo && 
                                      i.LocationId == toLocationId);

        if (inventory != null)
        {
            inventory.QtyOnHand += qty;
            inventory.UpdatedAt = DateTime.UtcNow;
            inventory.UpdatedBy = username;
            inventory.RowVersion += 1;
        }
        else
        {
            inventory = new Entities.Inventory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = itemId,
                LotNo = lotNo,
                LocationId = toLocationId,
                QtyOnHand = qty,
                QtyReserved = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username,
                RowVersion = 1
            };
            _context.Inventories.Add(inventory);
        }

        // 3. Create Inventory Transaction
        var invTrans = new Entities.InventoryTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = itemId,
            LotNo = lotNo,
            LocationId = toLocationId,
            TransactionType = "RECEIVE",
            Qty = qty,
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };
        _context.InventoryTransactions.Add(invTrans);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("CONCURRENCY_CONFLICT");
        }
    }
}
