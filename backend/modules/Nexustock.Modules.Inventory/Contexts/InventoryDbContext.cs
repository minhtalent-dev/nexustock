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
    public DbSet<Shipment> Shipments { get; set; } = null!;
    public DbSet<ShipmentItem> ShipmentItems { get; set; } = null!;
    public DbSet<PickTask> PickTasks { get; set; } = null!;
    public DbSet<PackingRecord> PackingRecords { get; set; } = null!;
    public DbSet<Stocktake> Stocktakes { get; set; } = null!;
    public DbSet<StocktakeItem> StocktakeItems { get; set; } = null!;
    public DbSet<StockAdjustment> StockAdjustments { get; set; } = null!;
    public DbSet<StockAdjustmentItem> StockAdjustmentItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Multi-Tenant Global Filters ---
        modelBuilder.Entity<Entities.Inventory>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<LocationLock>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<InventoryMovement>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<InventoryTransaction>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Lot>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Shipment>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ShipmentItem>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<PickTask>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<PackingRecord>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Stocktake>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StocktakeItem>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockAdjustment>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockAdjustmentItem>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

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

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ShipmentNo }).IsUnique().HasDatabaseName("uq_shipments_tenant_no");
        });

        modelBuilder.Entity<ShipmentItem>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ShipmentId, e.ItemId }).IsUnique().HasDatabaseName("uq_shipment_items_tenant_shipment_item");
        });

        modelBuilder.Entity<PackingRecord>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PackageNo }).IsUnique().HasDatabaseName("uq_packing_records_tenant_package");
        });

        modelBuilder.Entity<Stocktake>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StocktakeNo }).IsUnique().HasDatabaseName("uq_stocktakes_tenant_no");
        });

        modelBuilder.Entity<StocktakeItem>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StocktakeId, e.LocationId, e.ItemId, e.LotNo }).IsUnique().HasDatabaseName("uq_stocktake_items_tenant_take_loc_item_lot");
            entity.HasOne<Stocktake>().WithMany().HasForeignKey(e => e.StocktakeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.AdjustmentNo }).IsUnique().HasDatabaseName("uq_stock_adjustments_tenant_no");
            entity.HasOne<Stocktake>().WithMany().HasForeignKey(e => e.StocktakeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockAdjustmentItem>(entity =>
        {
            entity.HasOne<StockAdjustment>().WithMany().HasForeignKey(e => e.AdjustmentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
