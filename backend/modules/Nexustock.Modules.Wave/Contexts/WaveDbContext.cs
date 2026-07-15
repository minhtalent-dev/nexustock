using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Wave.Entities;

namespace Nexustock.Modules.Wave.Contexts;

public class WaveDbContext : DbContext
{
    public WaveDbContext(DbContextOptions<WaveDbContext> options) : base(options) {}

    public DbSet<PickingWave> PickingWaves => Set<PickingWave>();
    public DbSet<WaveItem> WaveItems => Set<WaveItem>();
    public DbSet<WavePickTask> WavePickTasks => Set<WavePickTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("wave");

        modelBuilder.Entity<PickingWave>(entity =>
        {
            entity.ToTable("picking_waves");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WaveNo).IsUnique();
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
            entity.HasMany(e => e.Items).WithOne().HasForeignKey(e => e.WaveId);
        });

        modelBuilder.Entity<WaveItem>(entity =>
        {
            entity.ToTable("wave_items");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<WavePickTask>(entity =>
        {
            entity.ToTable("wave_pick_tasks");
            entity.HasKey(e => e.Id);
        });
    }
}
