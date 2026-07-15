using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Rma.Entities;

namespace Nexustock.Modules.Rma.Contexts;

public class RmaDbContext : DbContext
{
    public RmaDbContext(DbContextOptions<RmaDbContext> options) : base(options) {}

    public DbSet<RmaRequest> RmaRequests => Set<RmaRequest>();
    public DbSet<RmaItem> RmaItems => Set<RmaItem>();
    public DbSet<RmaQcResult> RmaQcResults => Set<RmaQcResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rma");

        modelBuilder.Entity<RmaRequest>(entity =>
        {
            entity.ToTable("rma_requests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
            entity.HasMany(e => e.Items).WithOne().HasForeignKey(e => e.RmaId);
        });

        modelBuilder.Entity<RmaItem>(entity =>
        {
            entity.ToTable("rma_items");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<RmaQcResult>(entity =>
        {
            entity.ToTable("rma_qc_results");
            entity.HasKey(e => e.Id);
        });
    }
}
