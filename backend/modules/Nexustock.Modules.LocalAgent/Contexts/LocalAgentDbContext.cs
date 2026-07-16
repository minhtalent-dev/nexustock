using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.LocalAgent.Entities;
using Nexustock.Modules.LocalAgent.Services;

namespace Nexustock.Modules.LocalAgent.Contexts;

public class LocalAgentDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public LocalAgentDbContext(DbContextOptions<LocalAgentDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<AgentStation> AgentStations { get; set; } = null!;
    public DbSet<DeviceStatus> DeviceStatuses { get; set; } = null!;
    public DbSet<AgentPairingCode> AgentPairingCodes { get; set; } = null!;
    public DbSet<AgentConnectionEvent> AgentConnectionEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Multi-Tenant query filter
        modelBuilder.Entity<AgentStation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<DeviceStatus>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AgentPairingCode>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AgentConnectionEvent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // Fluent config for AgentStation
        modelBuilder.Entity<AgentStation>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StationCode }).IsUnique();
        });

        // Fluent config for DeviceStatus
        modelBuilder.Entity<DeviceStatus>(entity =>
        {
            entity.HasIndex(e => new { e.StationId, e.DeviceId }).IsUnique();
        });

        // Fluent config for AgentPairingCode
        modelBuilder.Entity<AgentPairingCode>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StationCode });
            entity.HasIndex(e => e.ExpiresAt);
        });

        // Fluent config for AgentConnectionEvent
        modelBuilder.Entity<AgentConnectionEvent>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
        });
    }
}
