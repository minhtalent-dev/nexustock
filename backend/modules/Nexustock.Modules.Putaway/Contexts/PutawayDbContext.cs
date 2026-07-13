using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Putaway.Entities;
using Nexustock.Modules.Putaway.Services;

namespace Nexustock.Modules.Putaway.Contexts;

public class PutawayDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public PutawayDbContext(DbContextOptions<PutawayDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<PutawayProposal> PutawayProposals { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Multi-Tenant query filter
        modelBuilder.Entity<PutawayProposal>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // Fluent Config
        modelBuilder.Entity<PutawayProposal>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("idx_putaway_proposals_tenant_status");
            entity.HasIndex(e => new { e.TenantId, e.LotId }).HasDatabaseName("idx_putaway_proposals_tenant_lot");
            
            // Concurrency token using xmin
            entity.Property(e => e.RowVersion).IsRowVersion();
        });
    }
}
