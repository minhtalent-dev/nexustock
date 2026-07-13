using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Rules.Entities;
using Nexustock.Modules.Rules.Services;

namespace Nexustock.Modules.Rules.Contexts;

public class RulesDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public RulesDbContext(DbContextOptions<RulesDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<RuleSet> RuleSets { get; set; } = null!;
    public DbSet<RuleCondition> RuleConditions { get; set; } = null!;
    public DbSet<RuleAction> RuleActions { get; set; } = null!;
    public DbSet<RuleExecutionLog> RuleExecutionLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Multi-Tenant Global Filters ---
        modelBuilder.Entity<RuleSet>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<RuleCondition>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<RuleAction>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<RuleExecutionLog>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // --- Fluent Configs ---
        modelBuilder.Entity<RuleSet>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("uq_rule_sets_tenant_code");
            entity.HasIndex(e => new { e.TenantId, e.Type }).HasDatabaseName("idx_rule_sets_tenant_type");
            
            // Concurrency token using xmin system column
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<RuleCondition>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.RuleSetId }).HasDatabaseName("idx_rule_conditions_tenant_ruleset");
            entity.HasOne<RuleSet>().WithMany().HasForeignKey(e => e.RuleSetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RuleAction>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.RuleSetId }).HasDatabaseName("idx_rule_actions_tenant_ruleset");
            entity.HasOne<RuleSet>().WithMany().HasForeignKey(e => e.RuleSetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RuleExecutionLog>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.RuleSetId }).HasDatabaseName("idx_rule_logs_tenant_ruleset");
            entity.HasIndex(e => new { e.TenantId, e.RuleTypeCode }).HasDatabaseName("idx_rule_logs_tenant_ruletype");
        });
    }
}
