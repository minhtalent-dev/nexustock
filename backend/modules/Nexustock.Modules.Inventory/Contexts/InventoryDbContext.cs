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
    public DbSet<ManualWeightOverride> ManualWeightOverrides { get; set; } = null!;
    public DbSet<Stocktake> Stocktakes { get; set; } = null!;
    public DbSet<StocktakeItem> StocktakeItems { get; set; } = null!;
    public DbSet<StockAdjustment> StockAdjustments { get; set; } = null!;
    public DbSet<StockAdjustmentItem> StockAdjustmentItems { get; set; } = null!;
    public DbSet<MobileDevice> MobileDevices { get; set; } = null!;
    public DbSet<ScanEvent> ScanEvents { get; set; } = null!;
    public DbSet<OfflineOperation> OfflineOperations { get; set; } = null!;
    public DbSet<MobileTask> MobileTasks { get; set; } = null!;
    public DbSet<AllocationReservation> AllocationReservations { get; set; } = null!;

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
        modelBuilder.Entity<ManualWeightOverride>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Stocktake>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StocktakeItem>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockAdjustment>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockAdjustmentItem>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<MobileDevice>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ScanEvent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<OfflineOperation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<MobileTask>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AllocationReservation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // --- Fluent Configs ---
        modelBuilder.Entity<Entities.Inventory>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ItemId, e.LotNo, e.LocationId, e.LpnId }).IsUnique().HasDatabaseName("uq_inventories_tenant_item_lot_location_lpn");
            entity.HasIndex(e => new { e.TenantId, e.LpnId }).HasDatabaseName("idx_inventories_tenant_lpn_id");
            entity.Property(e => e.QtyAvailable).HasComputedColumnSql("qty_on_hand - qty_reserved", stored: true);
            entity.ToTable("inventories", t =>
            {
                t.HasCheckConstraint("chk_inventory_balances_qty_reserved", "qty_reserved >= 0.0");
                t.HasCheckConstraint("chk_inventory_balances_qty_available", "qty_on_hand >= qty_reserved");
            });
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
            entity.HasIndex(e => new { e.TenantId, e.ManualOverrideId }).HasDatabaseName("idx_packing_records_tenant_manual_override");
        });

        modelBuilder.Entity<ManualWeightOverride>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ShipmentId, e.PackageNo, e.UsedAt }).HasDatabaseName("idx_manual_weight_overrides_lookup");
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

        modelBuilder.Entity<MobileDevice>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.DeviceCode }).IsUnique().HasDatabaseName("uq_mobile_devices_tenant_code");
        });

        modelBuilder.Entity<ScanEvent>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Context }).HasDatabaseName("idx_scan_events_tenant_context");
        });

        modelBuilder.Entity<OfflineOperation>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ClientOperationId }).IsUnique().HasDatabaseName("uq_offline_ops_tenant_client_op_id");
        });

        modelBuilder.Entity<MobileTask>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.AssignedUser, e.Status }).HasDatabaseName("idx_mobile_tasks_tenant_assigned");
            entity.HasIndex(e => new { e.TenantId, e.LocationId, e.Status }).HasDatabaseName("idx_mobile_tasks_loc_status");
        });

        modelBuilder.Entity<AllocationReservation>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("idx_allocation_reservations_tenant_status");
            entity.HasIndex(e => new { e.TenantId, e.ShipmentLineId }).HasDatabaseName("idx_allocation_reservations_shipment_line");
            entity.HasIndex(e => new { e.TenantId, e.InventoryBalanceId }).HasDatabaseName("idx_allocation_reservations_balance");
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("idx_allocation_reservations_expiry").HasFilter("\"status\" = 'ACTIVE'");
        });
    }
}
