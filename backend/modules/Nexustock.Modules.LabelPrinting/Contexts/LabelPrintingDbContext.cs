using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.LabelPrinting.Entities;
using Nexustock.Modules.LabelPrinting.Services;

namespace Nexustock.Modules.LabelPrinting.Contexts;

public class LabelPrintingDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public LabelPrintingDbContext(DbContextOptions<LabelPrintingDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<LabelTemplate> LabelTemplates { get; set; } = null!;
    public DbSet<PrintJob> PrintJobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LabelTemplate>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<PrintJob>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        modelBuilder.Entity<LabelTemplate>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.TemplateCode }).IsUnique().HasDatabaseName("uq_label_templates_tenant_code");
            entity.HasIndex(e => new { e.TenantId, e.IsActive }).HasDatabaseName("idx_label_templates_tenant_active");
        });

        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey }).IsUnique().HasDatabaseName("uq_print_jobs_tenant_idempotency");
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt }).HasDatabaseName("idx_print_jobs_tenant_created");
            entity.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("idx_print_jobs_tenant_status");
        });

        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        modelBuilder.Entity<LabelTemplate>().HasData(new LabelTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000002201"),
            TenantId = defaultTenantId,
            TemplateCode = "DEFAULT_LPN_ZPL",
            Name = "Default LPN Label",
            Language = "zpl",
            RawTemplate = "^XA^FO40,40^FD{{lpnCode}}^FS^FO40,90^FD{{itemCode}}^FS^XZ",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "SYSTEM",
            RowVersion = 1
        });
    }
}
