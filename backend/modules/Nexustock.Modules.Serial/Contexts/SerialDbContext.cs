using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Serial.Entities;
using Nexustock.Modules.Serial.Services;

namespace Nexustock.Modules.Serial.Contexts;

public class SerialDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public SerialDbContext(DbContextOptions<SerialDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<SerialNumber> SerialNumbers { get; set; } = null!;
    public DbSet<SerialEvent> SerialEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Multi-Tenant query filter
        modelBuilder.Entity<SerialNumber>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<SerialEvent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // Fluent Config for SerialNumber
        modelBuilder.Entity<SerialNumber>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ItemId, e.SerialNo })
                .IsUnique()
                .HasDatabaseName("uq_serials_tenant_item_no");

            entity.HasIndex(e => new { e.TenantId, e.LocationId })
                .HasDatabaseName("idx_serials_tenant_location");

            entity.HasIndex(e => new { e.TenantId, e.Status })
                .HasDatabaseName("idx_serials_tenant_status");

            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        // Fluent Config for SerialEvent
        modelBuilder.Entity<SerialEvent>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.SerialId })
                .HasDatabaseName("idx_serial_events_tenant_serial");
        });
    }
}
