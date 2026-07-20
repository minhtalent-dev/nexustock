using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.CrossDocking.Entities;

namespace Nexustock.Modules.CrossDocking.Contexts;

public class CrossDockingDbContext : DbContext
{
    public CrossDockingDbContext(DbContextOptions<CrossDockingDbContext> options) : base(options) { }

    public DbSet<CrossDockCandidate> Candidates => Set<CrossDockCandidate>();
    public DbSet<CrossDockEvent> Events => Set<CrossDockEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CrossDockCandidate>(e =>
        {
            e.ToTable("CrossDockCandidates");
            e.HasKey(x => x.Id);
            e.Property(x => x.QtyAvailable).HasColumnType("numeric(18,4)");
            e.Property(x => x.QtyRequested).HasColumnType("numeric(18,4)");
            e.Property(x => x.QtyMatched).HasColumnType("numeric(18,4)");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.CreatedBy).HasMaxLength(200);
            e.Property(x => x.UpdatedBy).HasMaxLength(200);
            e.Property(x => x.RejectedReason).HasColumnType("text");
            e.HasIndex(x => x.TenantId);
            e.HasIndex(x => x.LotId);
            e.HasIndex(x => x.WaveItemId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<CrossDockEvent>(e =>
        {
            e.ToTable("CrossDockEvents");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Actor).HasMaxLength(200);
            e.Property(x => x.TraceId).HasMaxLength(100);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.HasOne(x => x.Candidate)
             .WithMany()
             .HasForeignKey(x => x.CandidateId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TenantId, x.CandidateId });
            e.HasIndex(x => x.OccurredAt);
        });
    }
}
