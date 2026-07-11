using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Qc.Entities;
using Nexustock.Modules.Qc.Services;

namespace Nexustock.Modules.Qc.Contexts;

public class QcDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public QcDbContext(DbContextOptions<QcDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<QcRequest> QcRequests { get; set; } = null!;
    public DbSet<QcResult> QcResults { get; set; } = null!;
    public DbSet<MaterialHold> MaterialHolds { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Multi-Tenant Global Filters ---
        modelBuilder.Entity<QcRequest>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<QcResult>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<MaterialHold>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // --- Fluent Configs ---
        modelBuilder.Entity<QcRequest>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LotId, e.Status }).HasDatabaseName("idx_qc_requests_tenant_lot_status");
            entity.HasIndex(e => new { e.LotId }).HasDatabaseName("uq_qc_requests_pending_lot").HasFilter("\"status\" = 'Pending'").IsUnique();
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<QcResult>(entity =>
        {
            entity.HasOne(d => d.QcRequest)
                .WithMany()
                .HasForeignKey(d => d.QcRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaterialHold>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LotId, e.Status }).HasDatabaseName("idx_material_holds_tenant_lot_status");
        });
    }
}
