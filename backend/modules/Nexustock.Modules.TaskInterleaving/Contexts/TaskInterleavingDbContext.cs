using System;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.TaskInterleaving.Entities;

namespace Nexustock.Modules.TaskInterleaving.Contexts;

public class TaskInterleavingDbContext : DbContext
{
    private readonly Guid _currentTenantId;

    public TaskInterleavingDbContext(DbContextOptions<TaskInterleavingDbContext> options)
        : base(options)
    {
        _currentTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    public TaskInterleavingDbContext(DbContextOptions<TaskInterleavingDbContext> options, Nexustock.Modules.Inventory.Services.ITenantProvider tenantProvider)
        : base(options)
    {
        _currentTenantId = tenantProvider.TenantId;
    }

    public Guid CurrentTenantId => _currentTenantId;

    public DbSet<TaskRecommendation> TaskRecommendations { get; set; } = null!;
    public DbSet<TaskRecommendationCandidate> TaskRecommendationCandidates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("task_interleaving");

        // Multi-Tenant Global Filters
        modelBuilder.Entity<TaskRecommendation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<TaskRecommendationCandidate>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // Fluent Configurations
        modelBuilder.Entity<TaskRecommendation>(entity =>
        {
            entity.ToTable("task_recommendations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.SelectedTaskType).HasMaxLength(64);
            entity.Property(e => e.SourceTaskType).HasMaxLength(64);
            entity.Property(e => e.ReasonCode).HasMaxLength(64);
            entity.Property(e => e.DecisionNote).HasMaxLength(512);
            entity.Property(e => e.TraceId).HasMaxLength(128);
            entity.Property(e => e.AcceptIdempotencyKey).HasMaxLength(128);
            entity.Property(e => e.CreatedBy).HasMaxLength(128).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(128);
            entity.Property(e => e.SelectedScore).HasPrecision(18, 4);

            entity.HasIndex(e => new { e.TenantId, e.UserId, e.Status, e.CreatedAt }).HasDatabaseName("idx_recommendations_tenant_user_status_created");
            entity.HasIndex(e => new { e.TenantId, e.SelectedTaskType, e.SelectedTaskId }).HasDatabaseName("idx_recommendations_tenant_selected_task");
            entity.HasIndex(e => new { e.TenantId, e.ExpiresAt }).HasDatabaseName("idx_recommendations_tenant_expires");
            entity.HasIndex(e => new { e.TenantId, e.AcceptIdempotencyKey }).HasDatabaseName("idx_recommendations_tenant_idempotency").IsUnique().HasFilter("\"AcceptIdempotencyKey\" IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.UserId })
                .HasDatabaseName("uq_recommendations_tenant_user_open")
                .IsUnique()
                .HasFilter("\"Status\" = 'Open'");
        });

        modelBuilder.Entity<TaskRecommendationCandidate>(entity =>
        {
            entity.ToTable("task_recommendation_candidates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.OperationType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.TaskStatus).HasMaxLength(32).IsRequired();
            entity.Property(e => e.DistanceScore).HasPrecision(18, 4);
            entity.Property(e => e.AgeScore).HasPrecision(18, 4);
            entity.Property(e => e.PriorityScore).HasPrecision(18, 4);
            entity.Property(e => e.ContinuityScore).HasPrecision(18, 4);
            entity.Property(e => e.PenaltyScore).HasPrecision(18, 4);
            entity.Property(e => e.TotalScore).HasPrecision(18, 4);
            entity.Property(e => e.Explanation).HasColumnType("jsonb").IsRequired();

            entity.HasIndex(e => new { e.RecommendationId, e.TotalScore }).HasDatabaseName("idx_candidates_recommendation_score");
        });
    }
}
