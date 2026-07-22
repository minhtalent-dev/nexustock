using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nexustock.Modules.Inventory.Exceptions;

namespace Nexustock.Modules.Inventory.Interceptors;

/// <summary>
/// P36: chặn SaveChanges khi qty_on_hand / qty_reserved vi phạm invariant.
/// </summary>
public sealed class InventoryIntegrityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Validate(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Validate(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Validate(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<Entities.Inventory>())
        {
            if (entry.State is EntityState.Deleted) continue;

            var e = entry.Entity;
            if (e.QtyOnHand < 0 || e.QtyReserved < 0 || e.QtyReserved > e.QtyOnHand)
            {
                throw new InventoryInvariantException(
                    "INVENTORY_INVARIANT_VIOLATION",
                    $"Inventory {e.Id}: onHand={e.QtyOnHand}, reserved={e.QtyReserved}");
            }
        }
    }
}
