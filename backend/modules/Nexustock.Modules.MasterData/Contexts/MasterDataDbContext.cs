using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.MasterData.Entities;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Contexts;

public class MasterDataDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public MasterDataDbContext(DbContextOptions<MasterDataDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<TenantConfig> TenantConfigs { get; set; } = null!;
    public DbSet<Uom> Uoms { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductConfig> ProductConfigs { get; set; } = null!;
    public DbSet<Package> Packages { get; set; } = null!;
    public DbSet<Warehouse> Warehouses { get; set; } = null!;
    public DbSet<StorageZone> StorageZones { get; set; } = null!;
    public DbSet<StorageLocation> StorageLocations { get; set; } = null!;
    public DbSet<Partner> Partners { get; set; } = null!;
    public DbSet<ReasonCode> ReasonCodes { get; set; } = null!;
    public DbSet<ImportBatch> ImportBatches { get; set; } = null!;
    public DbSet<ImportBatchRow> ImportBatchRows { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Multi-Tenant Global Filters ---
        modelBuilder.Entity<TenantConfig>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Uom>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ProductConfig>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Package>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Warehouse>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StorageZone>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StorageLocation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Partner>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ReasonCode>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ImportBatch>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ImportBatchRow>().HasQueryFilter(e => e.Batch!.TenantId == CurrentTenantId);

        // --- Fluent Configs: Unique & Composite Indexes ---
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("uq_tenants_code");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_tenants_active");
        });

        modelBuilder.Entity<Uom>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("uq_uoms_tenant_code");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("uq_products_tenant_code");
            entity.HasIndex(e => new { e.TenantId, e.Barcode }).IsUnique().HasFilter("barcode IS NOT NULL").HasDatabaseName("uq_products_tenant_barcode");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.UomId }).IsUnique().HasDatabaseName("uq_packages_product_uom");
            entity.HasIndex(e => new { e.TenantId, e.Barcode }).IsUnique().HasFilter("barcode IS NOT NULL").HasDatabaseName("uq_packages_tenant_barcode");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("uq_warehouses_tenant_code");
        });

        modelBuilder.Entity<StorageZone>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.WarehouseId, e.Code }).IsUnique().HasDatabaseName("uq_storage_zones_tenant_warehouse_code");
        });

        modelBuilder.Entity<StorageLocation>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("uq_storage_locations_tenant_code");
            entity.HasIndex(e => new { e.TenantId, e.Code }).HasDatabaseName("idx_storage_locations_code");
        });

        modelBuilder.Entity<Partner>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("uq_partners_tenant_code");
        });

        modelBuilder.Entity<ReasonCode>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ReasonType, e.Code }).IsUnique().HasDatabaseName("uq_reason_codes_tenant_type_code");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("uq_permissions_code");
        });

        // --- Seed Data ---
        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        
        modelBuilder.Entity<Tenant>().HasData(new Tenant
        {
            Id = defaultTenantId,
            Code = "DEFAULT-TENANT",
            Name = "Default Tenant",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "SYSTEM",
            RowVersion = 1
        });

        modelBuilder.Entity<TenantConfig>().HasData(new TenantConfig
        {
            TenantId = defaultTenantId,
            FifoPolicyLevel = 2,
            LotNoPattern = "{YYYY}{MM}{DD}-{SEQ}",
            AllowNegativeStock = false
        });

        var uomPcsId = Guid.Parse("00000000-0000-0000-0000-000000000011");
        var uomBoxId = Guid.Parse("00000000-0000-0000-0000-000000000012");
        var uomPalletId = Guid.Parse("00000000-0000-0000-0000-000000000013");

        modelBuilder.Entity<Uom>().HasData(
            new Uom { Id = uomPcsId, TenantId = defaultTenantId, Code = "PCS", Name = "Cái", IsActive = true, CreatedBy = "SYSTEM", RowVersion = 1 },
            new Uom { Id = uomBoxId, TenantId = defaultTenantId, Code = "BOX", Name = "Hộp", IsActive = true, CreatedBy = "SYSTEM", RowVersion = 1 },
            new Uom { Id = uomPalletId, TenantId = defaultTenantId, Code = "PALLET", Name = "Pallet", IsActive = true, CreatedBy = "SYSTEM", RowVersion = 1 }
        );

        var whMainId = Guid.Parse("00000000-0000-0000-0000-000000000021");
        modelBuilder.Entity<Warehouse>().HasData(new Warehouse
        {
            Id = whMainId,
            TenantId = defaultTenantId,
            Code = "WH-MAIN",
            Name = "Kho chính",
            IsActive = true,
            CreatedBy = "SYSTEM",
            RowVersion = 1
        });

        var zoneStorageId = Guid.Parse("00000000-0000-0000-0000-000000000031");
        var zoneQcId = Guid.Parse("00000000-0000-0000-0000-000000000032");
        var zoneStagingId = Guid.Parse("00000000-0000-0000-0000-000000000033");

        modelBuilder.Entity<StorageZone>().HasData(
            new StorageZone { Id = zoneStorageId, TenantId = defaultTenantId, WarehouseId = whMainId, Code = "ZONE-STORAGE", Name = "Khu lưu trữ chính", ZoneType = "STORAGE", IsLocked = false, CreatedBy = "SYSTEM", RowVersion = 1 },
            new StorageZone { Id = zoneQcId, TenantId = defaultTenantId, WarehouseId = whMainId, Code = "ZONE-QC", Name = "Khu kiểm tra chất lượng", ZoneType = "QC", IsLocked = false, CreatedBy = "SYSTEM", RowVersion = 1 },
            new StorageZone { Id = zoneStagingId, TenantId = defaultTenantId, WarehouseId = whMainId, Code = "ZONE-STAGING", Name = "Khu trung chuyển", ZoneType = "STAGING", IsLocked = false, CreatedBy = "SYSTEM", RowVersion = 1 }
        );

        modelBuilder.Entity<StorageLocation>().HasData(
            new StorageLocation { Id = Guid.Parse("00000000-0000-0000-0000-000000000041"), TenantId = defaultTenantId, ZoneId = zoneStorageId, Code = "LOC-A-01", MaxCapacity = 1000, MaxVolume = 1000, XCoord = 1, YCoord = 1, ZCoord = 1, CreatedBy = "SYSTEM", RowVersion = 1 },
            new StorageLocation { Id = Guid.Parse("00000000-0000-0000-0000-000000000042"), TenantId = defaultTenantId, ZoneId = zoneStorageId, Code = "LOC-A-02", MaxCapacity = 1000, MaxVolume = 1000, XCoord = 1, YCoord = 2, ZCoord = 1, CreatedBy = "SYSTEM", RowVersion = 1 },
            new StorageLocation { Id = Guid.Parse("00000000-0000-0000-0000-000000000043"), TenantId = defaultTenantId, ZoneId = zoneQcId, Code = "LOC-QC-01", MaxCapacity = 500, MaxVolume = 500, XCoord = 2, YCoord = 1, ZCoord = 1, CreatedBy = "SYSTEM", RowVersion = 1 },
            new StorageLocation { Id = Guid.Parse("00000000-0000-0000-0000-000000000044"), TenantId = defaultTenantId, ZoneId = zoneQcId, Code = "LOC-QC-02", MaxCapacity = 500, MaxVolume = 500, XCoord = 2, YCoord = 2, ZCoord = 1, CreatedBy = "SYSTEM", RowVersion = 1 },
            new StorageLocation { Id = Guid.Parse("00000000-0000-0000-0000-000000000045"), TenantId = defaultTenantId, ZoneId = zoneStagingId, Code = "LOC-STG-01", MaxCapacity = 2000, MaxVolume = 2000, XCoord = 3, YCoord = 1, ZCoord = 1, CreatedBy = "SYSTEM", RowVersion = 1 }
        );

        modelBuilder.Entity<ReasonCode>().HasData(
            new ReasonCode { Id = Guid.Parse("00000000-0000-0000-0000-000000000051"), TenantId = defaultTenantId, Code = "HOLD-QC", ReasonType = "HOLD", Description = "Chờ QC kiểm tra chất lượng", CreatedBy = "SYSTEM", RowVersion = 1 },
            new ReasonCode { Id = Guid.Parse("00000000-0000-0000-0000-000000000052"), TenantId = defaultTenantId, Code = "ADJ-COUNT", ReasonType = "INVENTORY_ADJUSTMENT", Description = "Điều chỉnh số lượng kiểm kê", CreatedBy = "SYSTEM", RowVersion = 1 },
            new ReasonCode { Id = Guid.Parse("00000000-0000-0000-0000-000000000053"), TenantId = defaultTenantId, Code = "STOCKTAKE", ReasonType = "HOLD", Description = "Khóa vị trí phục vụ kiểm kê chu kỳ", CreatedBy = "SYSTEM", RowVersion = 1 }
        );

        modelBuilder.Entity<Permission>().HasData(Nexustock.Modules.MasterData.Permissions.AppPermissions.All);
    }
}
