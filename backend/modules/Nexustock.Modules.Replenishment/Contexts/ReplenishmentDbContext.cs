using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Replenishment.Entities;
using Nexustock.Modules.Replenishment.Services;

namespace Nexustock.Modules.Replenishment.Contexts;

public class ReplenishmentDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public ReplenishmentDbContext(DbContextOptions<ReplenishmentDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<ReplenishmentRule> ReplenishmentRules { get; set; } = null!;
    public DbSet<ReplenishmentTask> ReplenishmentTasks { get; set; } = null!;
    public DbSet<Lot> Lots { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Multi-Tenant query filter
        modelBuilder.Entity<ReplenishmentRule>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ReplenishmentTask>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Lot>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // Fluent Config for ReplenishmentRule
        modelBuilder.Entity<ReplenishmentRule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ItemId, e.LocationId })
                .IsUnique()
                .HasDatabaseName("idx_replenishment_rules_tenant_item_location");

            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        // Fluent Config for ReplenishmentTask
        modelBuilder.Entity<ReplenishmentTask>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Status })
                .HasDatabaseName("idx_replenishment_tasks_tenant_status");

            entity.HasIndex(e => new { e.TenantId, e.TargetLocationId })
                .HasDatabaseName("idx_replenishment_tasks_tenant_target_location");

            entity.Property(e => e.RowVersion).IsRowVersion();
        });
    }
}
