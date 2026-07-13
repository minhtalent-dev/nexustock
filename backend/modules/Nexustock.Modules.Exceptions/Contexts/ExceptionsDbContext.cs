using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Exceptions.Entities;
using Nexustock.Modules.Exceptions.Services;

namespace Nexustock.Modules.Exceptions.Contexts;

public class ExceptionsDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public ExceptionsDbContext(DbContextOptions<ExceptionsDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<OperationalException> OperationalExceptions { get; set; } = null!;
    public DbSet<ExceptionEvent> ExceptionEvents { get; set; } = null!;
    public DbSet<ExceptionAssignment> ExceptionAssignments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Multi-Tenant Global Filters ---
        modelBuilder.Entity<OperationalException>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ExceptionEvent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ExceptionAssignment>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // --- Fluent Configs ---
        modelBuilder.Entity<OperationalException>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("uq_operational_exceptions_tenant_code");
            entity.HasIndex(e => new { e.TenantId, e.ReferenceId }).HasDatabaseName("idx_exceptions_tenant_reference");
            entity.HasIndex(e => new { e.TenantId, e.LocationId }).HasDatabaseName("idx_exceptions_tenant_location");
            
            // Concurrency token using xmin system column
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ExceptionEvent>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ExceptionId }).HasDatabaseName("idx_exception_events_tenant_exception");
            entity.HasOne<OperationalException>().WithMany().HasForeignKey(e => e.ExceptionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExceptionAssignment>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ExceptionId }).HasDatabaseName("idx_exception_assign_tenant_exception");
            entity.HasOne<OperationalException>().WithMany().HasForeignKey(e => e.ExceptionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
