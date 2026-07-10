using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Inbound.Services;

namespace Nexustock.Modules.Inbound.Contexts;

public class InboundDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public InboundDbContext(DbContextOptions<InboundDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<InboundOrder> InboundOrders { get; set; } = null!;
    public DbSet<InboundOrderItem> InboundOrderItems { get; set; } = null!;
    public DbSet<Lot> Lots { get; set; } = null!;
    public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Multi-Tenant Global Filters ---
        modelBuilder.Entity<InboundOrder>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<InboundOrderItem>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Lot>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<InventoryTransaction>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // --- Fluent Configs ---
        modelBuilder.Entity<InboundOrder>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.OrderNo }).IsUnique().HasDatabaseName("uq_inbound_orders_tenant_orderno");
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<InboundOrderItem>(entity =>
        {
            entity.HasOne(d => d.InboundOrder)
                .WithMany(p => p.Items)
                .HasForeignKey(d => d.InboundOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Lot>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LotNo, e.ItemId }).IsUnique().HasDatabaseName("uq_lots_tenant_lotno_itemid");
            entity.Property(e => e.QcStatus).HasConversion<string>();
        });

        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LotNo, e.ItemId }).HasDatabaseName("idx_inv_trans_tenant_lot_item");
        });
    }
}
