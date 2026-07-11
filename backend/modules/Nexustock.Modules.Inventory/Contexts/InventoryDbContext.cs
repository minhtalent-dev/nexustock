using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.Inventory.Services;

namespace Nexustock.Modules.Inventory.Contexts;

public class InventoryDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<Entities.Inventory> Inventories { get; set; } = null!;
    public DbSet<LocationLock> LocationLocks { get; set; } = null!;
    public DbSet<InventoryMovement> InventoryMovements { get; set; } = null!;
    public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
    public DbSet<Lot> Lots { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Multi-Tenant Global Filters ---
        modelBuilder.Entity<Entities.Inventory>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<LocationLock>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<InventoryMovement>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<InventoryTransaction>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Lot>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // --- Fluent Configs ---
        modelBuilder.Entity<Entities.Inventory>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ItemId, e.LotNo, e.LocationId }).IsUnique().HasDatabaseName("uq_inventories_tenant_item_lot_location");
            entity.Property(e => e.QtyAvailable).HasComputedColumnSql("qty_on_hand - qty_reserved", stored: true);
        });

        modelBuilder.Entity<LocationLock>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LocationId }).IsUnique().HasDatabaseName("uq_location_locks_tenant_location");
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("idx_inv_movements_tenant_status");
        });

        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LotNo, e.ItemId }).HasDatabaseName("idx_inv_trans_tenant_lot_item_inv");
        });
    }
}
