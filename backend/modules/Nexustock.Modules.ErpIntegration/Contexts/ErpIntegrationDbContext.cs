using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.ErpIntegration.Entities;
using Nexustock.Modules.ErpIntegration.Services;

namespace Nexustock.Modules.ErpIntegration.Contexts;

public class ErpIntegrationDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public ErpIntegrationDbContext(DbContextOptions<ErpIntegrationDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<IntegrationMessage> IntegrationMessages { get; set; } = null!;
    public DbSet<IntegrationMapping> IntegrationMappings { get; set; } = null!;
    public DbSet<IntegrationImportJob> IntegrationImportJobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply Multi-Tenant query filters
        modelBuilder.Entity<IntegrationMessage>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<IntegrationMapping>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<IntegrationImportJob>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        modelBuilder.Entity<IntegrationMessage>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey }).IsUnique().HasDatabaseName("uq_integration_messages_tenant_idem");
            entity.HasIndex(e => new { e.TenantId, e.PayloadHash }).HasDatabaseName("idx_integration_messages_tenant_hash");
            entity.HasIndex(e => new { e.TenantId, e.ExternalSystem }).HasDatabaseName("idx_integration_messages_tenant_system");
            entity.HasIndex(e => new { e.TenantId, e.ExternalReference }).HasDatabaseName("idx_integration_messages_tenant_ref");
            entity.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("idx_integration_messages_tenant_status");
            entity.HasIndex(e => new { e.TenantId, e.TraceId }).HasDatabaseName("idx_integration_messages_tenant_trace");
        });

        modelBuilder.Entity<IntegrationMapping>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ExternalSystem, e.MappingType, e.ExternalCode }).IsUnique().HasDatabaseName("uq_integration_mappings_tenant_sys_type_code");
            entity.HasIndex(e => new { e.TenantId, e.InternalCode }).HasDatabaseName("idx_integration_mappings_tenant_internal");
        });

        modelBuilder.Entity<IntegrationImportJob>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ImportType }).HasDatabaseName("idx_integration_import_jobs_tenant_type");
            entity.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("idx_integration_import_jobs_tenant_status");
            entity.HasIndex(e => new { e.TenantId, e.TraceId }).HasDatabaseName("idx_integration_import_jobs_tenant_trace");
        });
    }
}
