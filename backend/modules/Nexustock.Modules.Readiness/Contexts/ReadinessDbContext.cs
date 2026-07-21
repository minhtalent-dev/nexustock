using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Readiness.Entities;

namespace Nexustock.Modules.Readiness.Contexts;

public class ReadinessDbContext : DbContext
{
    private readonly Guid _currentTenantId;

    public ReadinessDbContext(DbContextOptions<ReadinessDbContext> options)
        : base(options)
    {
        _currentTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    public ReadinessDbContext(DbContextOptions<ReadinessDbContext> options, Nexustock.Modules.Inventory.Services.ITenantProvider tenantProvider)
        : base(options)
    {
        _currentTenantId = tenantProvider.TenantId;
    }

    public Guid CurrentTenantId => _currentTenantId;

    public DbSet<UatRun> UatRuns { get; set; } = null!;
    public DbSet<CutoverLog> CutoverLogs { get; set; } = null!;
    public DbSet<IncidentDrill> IncidentDrills { get; set; } = null!;
    public DbSet<CutoverFreezeState> CutoverFreezeStates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("readiness");

        modelBuilder.Entity<UatRun>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<CutoverLog>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<IncidentDrill>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<CutoverFreezeState>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        modelBuilder.Entity<UatRun>(entity =>
        {
            entity.ToTable("uat_runs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScenarioCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.ResultNote).HasMaxLength(1024);
            entity.Property(e => e.SignedOffBy).HasMaxLength(128);
            entity.Property(e => e.EvidenceUrl).HasMaxLength(512);
            entity.Property(e => e.TraceId).HasMaxLength(128);
            entity.Property(e => e.CreatedBy).HasMaxLength(128).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(128);
            entity.HasIndex(e => new { e.TenantId, e.ScenarioCode, e.CreatedAt })
                .HasDatabaseName("idx_uat_runs_tenant_scenario_created")
                .IsDescending(false, false, true);
        });

        modelBuilder.Entity<CutoverLog>(entity =>
        {
            entity.ToTable("cutover_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StepCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Actor).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(1024);
            entity.Property(e => e.TraceId).HasMaxLength(128);
            entity.HasIndex(e => new { e.TenantId, e.StepCode, e.StartedAt })
                .HasDatabaseName("idx_cutover_logs_tenant_step_started")
                .IsDescending(false, false, true);
        });

        modelBuilder.Entity<IncidentDrill>(entity =>
        {
            entity.ToTable("incident_drills");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScenarioCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ConductedBy).HasMaxLength(128).IsRequired();
            entity.Property(e => e.EvidenceNote).HasMaxLength(1024);
            entity.Property(e => e.TraceId).HasMaxLength(128);
        });

        modelBuilder.Entity<CutoverFreezeState>(entity =>
        {
            entity.ToTable("cutover_freeze_states");
            entity.HasKey(e => e.TenantId);
            entity.Property(e => e.FrozenBy).HasMaxLength(128);
            entity.Property(e => e.Reason).HasMaxLength(512);
            entity.Property(e => e.CreatedBy).HasMaxLength(128).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(128);
        });
    }
}
