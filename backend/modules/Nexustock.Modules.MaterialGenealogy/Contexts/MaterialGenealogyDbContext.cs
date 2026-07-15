using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.MaterialGenealogy.Entities;

namespace Nexustock.Modules.MaterialGenealogy.Contexts;

public class MaterialGenealogyDbContext : DbContext
{
    public MaterialGenealogyDbContext(DbContextOptions<MaterialGenealogyDbContext> options) : base(options) {}

    public DbSet<LotRelation> LotRelations => Set<LotRelation>();
    public DbSet<GenealogyEvent> GenealogyEvents => Set<GenealogyEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("genealogy");

        modelBuilder.Entity<LotRelation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.ParentLotId });
            entity.HasIndex(e => new { e.TenantId, e.ChildLotId });
            entity.HasIndex(e => new { e.TenantId, e.ParentLotId, e.ChildLotId }).IsUnique();
        });

        modelBuilder.Entity<GenealogyEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.LotId });
        });
    }
}
