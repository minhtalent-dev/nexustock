using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Lpn.Entities;
using Nexustock.Modules.Lpn.Services;

namespace Nexustock.Modules.Lpn.Contexts;

public class LpnDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public LpnDbContext(DbContextOptions<LpnDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<Entities.Lpn> Lpns { get; set; } = null!;
    public DbSet<LpnEvent> LpnEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Multi-Tenant query filter
        modelBuilder.Entity<Entities.Lpn>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<LpnEvent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // Fluent Config for Lpn
        modelBuilder.Entity<Entities.Lpn>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LpnNo })
                .IsUnique()
                .HasDatabaseName("idx_lpns_tenant_lpn_no");

            entity.HasIndex(e => new { e.TenantId, e.LocationId })
                .HasDatabaseName("idx_lpns_tenant_location_id");

            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        // Fluent Config for LpnEvent
        modelBuilder.Entity<LpnEvent>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.LpnId })
                .HasDatabaseName("idx_lpn_events_tenant_lpn_id");
        });
    }
}
