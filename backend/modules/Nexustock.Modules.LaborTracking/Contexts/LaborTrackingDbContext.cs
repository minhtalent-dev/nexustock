using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.LaborTracking.Entities;

namespace Nexustock.Modules.LaborTracking.Contexts;

public class LaborTrackingDbContext : DbContext
{
    public LaborTrackingDbContext(DbContextOptions<LaborTrackingDbContext> options) : base(options) { }

    public DbSet<LaborSession> LaborSessions => Set<LaborSession>();
    public DbSet<LaborSessionEvent> LaborSessionEvents => Set<LaborSessionEvent>();
    public DbSet<LaborShift> LaborShifts => Set<LaborShift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LaborSession>(e =>
        {
            e.ToTable("labor_sessions");
            e.HasKey(x => x.Id);

            e.Property(x => x.SourceTaskType).HasMaxLength(50);
            e.Property(x => x.ReferenceType).HasMaxLength(100);
            e.Property(x => x.UserId).HasMaxLength(100);
            e.Property(x => x.OperationType).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);

            // Indexes
            e.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.ShiftId });
            e.HasIndex(x => new { x.TenantId, x.ZoneId, x.StartedAt });
            e.HasIndex(x => new { x.TenantId, x.SourceTaskType, x.SourceTaskId });

            // Unique index: Mỗi user trong một tenant chỉ có tối đa 1 active session (Running hoặc Paused)
            e.HasIndex(x => new { x.TenantId, x.UserId })
             .HasDatabaseName("IX_labor_sessions_active_user")
             .HasFilter("\"status\" IN ('Running', 'Paused')")
             .IsUnique();
        });

        modelBuilder.Entity<LaborSessionEvent>(e =>
        {
            e.ToTable("labor_session_events");
            e.HasKey(x => x.Id);

            e.Property(x => x.EventType).HasMaxLength(50);
            e.Property(x => x.Actor).HasMaxLength(100);
            e.Property(x => x.TraceId).HasMaxLength(100);
            e.Property(x => x.Payload).HasColumnType("jsonb");

            e.HasIndex(x => new { x.TenantId, x.SessionId });
            e.HasIndex(x => x.OccurredAt);
        });

        modelBuilder.Entity<LaborShift>(e =>
        {
            e.ToTable("labor_shifts");
            e.HasKey(x => x.Id);

            e.Property(x => x.UserId).HasMaxLength(100);
            e.Property(x => x.ShiftCode).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);

            e.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
            e.HasIndex(x => x.ShiftCode).IsUnique();
        });
    }
}
