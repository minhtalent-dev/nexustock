using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Observability.Entities;

namespace Nexustock.Modules.Observability.Contexts;

public class ObservabilityDbContext : DbContext
{
    public ObservabilityDbContext(DbContextOptions<ObservabilityDbContext> options) : base(options) { }

    public DbSet<ActivityTimelineEntry> ActivityTimelineEntries => Set<ActivityTimelineEntry>();
    public DbSet<OperationalAlert> OperationalAlerts => Set<OperationalAlert>();
    public DbSet<KpiSnapshot> KpiSnapshots => Set<KpiSnapshot>();
    public DbSet<TraceLog> TraceLogs => Set<TraceLog>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FeatureFlag>(e =>
        {
            e.ToTable("FeatureFlags");
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Enabled).IsRequired();
            e.Property(x => x.RolloutPercentage).IsRequired();
            e.Property(x => x.WhitelistUserIds).HasColumnType("text");
            e.Property(x => x.Description).HasColumnType("text");
            e.Property(x => x.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<ActivityTimelineEntry>(e =>
        {
            e.ToTable("ActivityTimeline");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityId).IsRequired();
            e.Property(x => x.EventType).HasMaxLength(80).IsRequired();
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasColumnType("text");
            e.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            e.Property(x => x.TraceId).HasMaxLength(80).IsRequired();
            e.Property(x => x.MetadataJson).HasColumnType("text");
            e.Property(x => x.CreatedAt).IsRequired();

            e.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId, x.CreatedAt });
            e.HasIndex(x => new { x.TenantId, x.TraceId });
            e.HasIndex(x => x.ActorUserId);
        });

        modelBuilder.Entity<OperationalAlert>(e =>
        {
            e.ToTable("OperationalAlerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired();
            e.Property(x => x.AlertType).HasMaxLength(80).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Message).HasColumnType("text").IsRequired();
            e.Property(x => x.SourceModule).HasMaxLength(80).IsRequired();
            e.Property(x => x.SourceEntityType).HasMaxLength(50);
            e.Property(x => x.TraceId).HasMaxLength(80);
            e.Property(x => x.MetricValue).HasColumnType("decimal(18,4)");
            e.Property(x => x.ThresholdValue).HasColumnType("decimal(18,4)");
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();

            e.HasIndex(x => new { x.TenantId, x.Status, x.Severity, x.CreatedAt });
            e.HasIndex(x => new { x.TenantId, x.AlertType, x.Status });
            e.HasIndex(x => x.TraceId);
        });

        modelBuilder.Entity<KpiSnapshot>(e =>
        {
            e.ToTable("KpiSnapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired();
            e.Property(x => x.MetricKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.MetricGroup).HasMaxLength(50).IsRequired();
            e.Property(x => x.Value).HasColumnType("decimal(18,4)").IsRequired();
            e.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            e.Property(x => x.PeriodStart).IsRequired();
            e.Property(x => x.PeriodEnd).IsRequired();
            e.Property(x => x.SourceModule).HasMaxLength(80).IsRequired();
            e.Property(x => x.ComputedAt).IsRequired();
            e.Property(x => x.MetadataJson).HasColumnType("text");

            e.HasIndex(x => new { x.TenantId, x.MetricGroup, x.MetricKey, x.PeriodEnd });
            e.HasIndex(x => x.ComputedAt);
        });

        modelBuilder.Entity<TraceLog>(e =>
        {
            e.ToTable("TraceLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.TraceId).HasMaxLength(80).IsRequired();
            e.Property(x => x.SpanName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Source).HasMaxLength(80).IsRequired();
            e.Property(x => x.Level).HasMaxLength(20).IsRequired();
            e.Property(x => x.Message).HasColumnType("text").IsRequired();
            e.Property(x => x.MetadataJson).HasColumnType("text");
            e.Property(x => x.CreatedAt).IsRequired();

            e.HasIndex(x => new { x.TraceId, x.CreatedAt });
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });
    }
}
