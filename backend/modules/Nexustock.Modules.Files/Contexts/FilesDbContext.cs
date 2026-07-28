using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Inventory.Services;

namespace Nexustock.Modules.Files.Contexts;

public class FilesDbContext : DbContext
{
    private readonly Guid _currentTenantId;

    public FilesDbContext(DbContextOptions<FilesDbContext> options)
        : base(options)
    {
        _currentTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    public FilesDbContext(DbContextOptions<FilesDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _currentTenantId = tenantProvider.TenantId;
    }

    public Guid CurrentTenantId => _currentTenantId;

    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();
    public DbSet<FilePendingUpload> FilePendingUploads => Set<FilePendingUpload>();
    public DbSet<FileStorageSettings> FileStorageSettings => Set<FileStorageSettings>();
    public DbSet<FileStorageMigrateJob> FileStorageMigrateJobs => Set<FileStorageMigrateJob>();
    public DbSet<FileStorageMigrateJobError> FileStorageMigrateJobErrors => Set<FileStorageMigrateJobError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("files");

        modelBuilder.Entity<FileAttachment>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<FilePendingUpload>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<FileStorageSettings>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<FileStorageMigrateJob>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        modelBuilder.Entity<FileAttachment>(entity =>
        {
            entity.ToTable("file_attachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Kind).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Provider).HasMaxLength(32).IsRequired();
            entity.Property(e => e.StorageKey).HasMaxLength(512).IsRequired();
            entity.Property(e => e.PublicUrl).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(128);
            entity.Property(e => e.ThumbnailKey).HasMaxLength(512);
            entity.Property(e => e.ObjectsPurgedAt);
            entity.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId })
                .HasDatabaseName("ix_file_attachments_entity")
                .HasFilter("\"DeletedAt\" IS NULL");
        });

        modelBuilder.Entity<FilePendingUpload>(entity =>
        {
            entity.ToTable("file_pending_uploads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Kind).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Provider).HasMaxLength(32).IsRequired();
            entity.Property(e => e.StorageKey).HasMaxLength(512).IsRequired();
            entity.Property(e => e.LegacyUrl).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(128);
            entity.Property(e => e.ThumbnailKey).HasMaxLength(512);
            entity.HasIndex(e => new { e.TenantId, e.Status, e.ExpiresAt }).HasDatabaseName("ix_file_pending_uploads_tenant_status_exp");
            entity.HasIndex(e => new { e.TenantId, e.StorageKey }).IsUnique().HasDatabaseName("ix_file_pending_uploads_tenant_key");
            entity.HasIndex(e => e.AttachmentId).IsUnique().HasFilter("\"AttachmentId\" IS NOT NULL").HasDatabaseName("ix_file_pending_uploads_attachment");
        });

        modelBuilder.Entity<FileStorageSettings>(entity =>
        {
            entity.ToTable("file_storage_settings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId).IsUnique();
            entity.Property(e => e.ActiveProvider).HasMaxLength(32).IsRequired();
            entity.Property(e => e.PublicBaseUrl).HasMaxLength(1024);
            entity.Property(e => e.LocalPathOverride).HasMaxLength(1024);
            entity.Property(e => e.UpdatedBy).HasMaxLength(128);
            entity.Property(e => e.LastTestMessage).HasMaxLength(512);
        });

        modelBuilder.Entity<FileStorageMigrateJob>(entity =>
        {
            entity.ToTable("file_storage_migrate_jobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceProvider).HasMaxLength(32);
            entity.Property(e => e.TargetProvider).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Mode).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.ErrorSummary).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).HasMaxLength(128);
            entity.Property(e => e.EligibleIdsJson).HasColumnType("jsonb");
            entity.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("ix_migrate_jobs_tenant_status");
        });

        modelBuilder.Entity<FileStorageMigrateJobError>(entity =>
        {
            entity.ToTable("file_storage_migrate_job_errors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();
            entity.HasIndex(e => e.JobId).HasDatabaseName("ix_migrate_job_errors_job");
        });
    }
}
