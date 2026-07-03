using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Entities;

namespace Nexustock.Modules.MasterData.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken);
    Task<(OperationResult Result, ProductDto? Item)> CreateProductAsync(UpsertProductRequest request, CancellationToken cancellationToken);
    Task<(OperationResult Result, ProductDto? Item)> UpdateProductAsync(Guid id, UpsertProductRequest request, CancellationToken cancellationToken);
    Task<OperationResult> DeleteProductAsync(Guid id, CancellationToken cancellationToken);
}

public class ProductService : IProductService
{
    private readonly MasterDataDbContext _db;

    public ProductService(MasterDataDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(x => x.BaseUom)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(keyword) || x.Name.ToUpper().Contains(keyword) || (x.Barcode != null && x.Barcode.ToUpper().Contains(keyword)));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var productIds = items.Select(x => x.Id).ToList();
        var configs = await _db.ProductConfigs
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var packages = await _db.Packages
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .ToListAsync(cancellationToken);
        var uoms = await _db.Uoms.AsNoTracking().ToDictionaryAsync(x => x.Id, cancellationToken);

        var dtos = items
            .Select(x => MapToDto(
                x,
                configs.TryGetValue(x.Id, out var config) ? config : null,
                packages.Where(p => p.ProductId == x.Id).ToList(),
                uoms))
            .ToList();
        return new PagedResult<ProductDto>(dtos, total, page, pageSize);
    }

    public async Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(x => x.BaseUom)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (product is null) return null;

        var config = await _db.ProductConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == id, cancellationToken);
        var packages = await _db.Packages.AsNoTracking().Where(x => x.ProductId == id).ToListAsync(cancellationToken);
        var uoms = await _db.Uoms.AsNoTracking().ToDictionaryAsync(x => x.Id, cancellationToken);

        return MapToDto(product, config, packages, uoms);
    }

    public async Task<(OperationResult Result, ProductDto? Item)> CreateProductAsync(UpsertProductRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateProductRequest(request);
        if (!validation.Success) return (validation, null);

        if (!await _db.Uoms.AnyAsync(x => x.Id == request.BaseUomId, cancellationToken))
            return (OperationResult.Fail("UOM_NOT_FOUND", "Đơn vị tính cơ sở không tồn tại."), null);

        var productId = Guid.NewGuid();
        var tenantId = _db.CurrentTenantId;

        var product = new Product
        {
            Id = productId,
            TenantId = tenantId,
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim().ToUpperInvariant(),
            BaseUomId = request.BaseUomId,
            IsActive = request.IsActive
        };

        var config = new ProductConfig
        {
            ProductId = productId,
            TenantId = tenantId,
            IqcCheckType = request.Config.IqcCheckType.Trim().ToUpperInvariant(),
            VendorInnerLotCtl = request.Config.VendorInnerLotCtl,
            IsWafer = request.Config.IsWafer,
            LotValidationRegex = request.Config.LotValidationRegex,
            MinStock = request.Config.MinStock,
            MaxStock = request.Config.MaxStock,
            WeightClass = request.Config.WeightClass.Trim().ToUpperInvariant(),
            RotationSpeed = request.Config.RotationSpeed.Trim().ToUpperInvariant(),
            TrackSerial = request.Config.TrackSerial,
            Length = request.Config.Length,
            Width = request.Config.Width,
            Height = request.Config.Height,
            Weight = request.Config.Weight
        };

        var packages = request.Packages.Select(p => new Package
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            PackageName = p.PackageName.Trim(),
            Barcode = string.IsNullOrWhiteSpace(p.Barcode) ? null : p.Barcode.Trim().ToUpperInvariant(),
            UomId = p.UomId,
            ConversionFactor = p.ConversionFactor,
            IsActive = p.IsActive
        }).ToList();

        _db.Products.Add(product);
        _db.ProductConfigs.Add(config);
        _db.Packages.AddRange(packages);

        var saveResult = await SaveChangesAsync(cancellationToken);
        if (!saveResult.Success) return (saveResult, null);

        return (saveResult, await GetProductAsync(productId, cancellationToken));
    }

    public async Task<(OperationResult Result, ProductDto? Item)> UpdateProductAsync(Guid id, UpsertProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null) return (OperationResult.Fail("NOT_FOUND", "Không tìm thấy vật tư."), null);

        if (request.RowVersion is null || request.RowVersion.Value != product.RowVersion)
            return (OperationResult.Fail("CONFLICT", "Dữ liệu đã thay đổi, vui lòng tải lại trước khi lưu."), null);

        var validation = ValidateProductRequest(request);
        if (!validation.Success) return (validation, null);

        if (!await _db.Uoms.AnyAsync(x => x.Id == request.BaseUomId, cancellationToken))
            return (OperationResult.Fail("UOM_NOT_FOUND", "Đơn vị tính cơ sở không tồn tại."), null);

        product.Code = request.Code.Trim().ToUpperInvariant();
        product.Name = request.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim().ToUpperInvariant();
        product.BaseUomId = request.BaseUomId;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        product.RowVersion++;

        var config = await _db.ProductConfigs.FirstOrDefaultAsync(x => x.ProductId == id, cancellationToken);
        if (config is null)
        {
            config = new ProductConfig { ProductId = id, TenantId = _db.CurrentTenantId };
            _db.ProductConfigs.Add(config);
        }

        config.IqcCheckType = request.Config.IqcCheckType.Trim().ToUpperInvariant();
        config.VendorInnerLotCtl = request.Config.VendorInnerLotCtl;
        config.IsWafer = request.Config.IsWafer;
        config.LotValidationRegex = request.Config.LotValidationRegex;
        config.MinStock = request.Config.MinStock;
        config.MaxStock = request.Config.MaxStock;
        config.WeightClass = request.Config.WeightClass.Trim().ToUpperInvariant();
        config.RotationSpeed = request.Config.RotationSpeed.Trim().ToUpperInvariant();
        config.TrackSerial = request.Config.TrackSerial;
        config.Length = request.Config.Length;
        config.Width = request.Config.Width;
        config.Height = request.Config.Height;
        config.Weight = request.Config.Weight;

        // Cập nhật Packages
        var existingPackages = await _db.Packages.Where(x => x.ProductId == id).ToListAsync(cancellationToken);
        
        // Xóa package không còn trong request
        var requestPackageIds = request.Packages.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToList();
        var packagesToDelete = existingPackages.Where(p => !requestPackageIds.Contains(p.Id)).ToList();
        _db.Packages.RemoveRange(packagesToDelete);

        // Thêm mới / Cập nhật package
        foreach (var pReq in request.Packages)
        {
            if (pReq.Id.HasValue)
            {
                var existingPkg = existingPackages.FirstOrDefault(x => x.Id == pReq.Id.Value);
                if (existingPkg != null)
                {
                    existingPkg.PackageName = pReq.PackageName.Trim();
                    existingPkg.Barcode = string.IsNullOrWhiteSpace(pReq.Barcode) ? null : pReq.Barcode.Trim().ToUpperInvariant();
                    existingPkg.UomId = pReq.UomId;
                    existingPkg.ConversionFactor = pReq.ConversionFactor;
                    existingPkg.IsActive = pReq.IsActive;
                    existingPkg.UpdatedAt = DateTimeOffset.UtcNow;
                    existingPkg.RowVersion++;
                }
            }
            else
            {
                var newPkg = new Package
                {
                    Id = Guid.NewGuid(),
                    TenantId = _db.CurrentTenantId,
                    ProductId = id,
                    PackageName = pReq.PackageName.Trim(),
                    Barcode = string.IsNullOrWhiteSpace(pReq.Barcode) ? null : pReq.Barcode.Trim().ToUpperInvariant(),
                    UomId = pReq.UomId,
                    ConversionFactor = pReq.ConversionFactor,
                    IsActive = pReq.IsActive
                };
                _db.Packages.Add(newPkg);
            }
        }

        var saveResult = await SaveChangesAsync(cancellationToken);
        if (!saveResult.Success) return (saveResult, null);

        return (saveResult, await GetProductAsync(id, cancellationToken));
    }

    public async Task<OperationResult> DeleteProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null) return OperationResult.Fail("NOT_FOUND", "Không tìm thấy vật tư.");

        var config = await _db.ProductConfigs.FirstOrDefaultAsync(x => x.ProductId == id, cancellationToken);
        if (config != null) _db.ProductConfigs.Remove(config);

        var packages = await _db.Packages.Where(x => x.ProductId == id).ToListAsync(cancellationToken);
        _db.Packages.RemoveRange(packages);

        _db.Products.Remove(product);
        return await SaveChangesAsync(cancellationToken);
    }

    private static OperationResult ValidateProductRequest(UpsertProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return OperationResult.Fail("CODE_REQUIRED", "Mã vật tư không được trống.");
        if (string.IsNullOrWhiteSpace(request.Name)) return OperationResult.Fail("NAME_REQUIRED", "Tên vật tư không được trống.");
        if (request.Config is null) return OperationResult.Fail("CONFIG_REQUIRED", "Cấu hình vật tư không được trống.");

        // Validate packages
        foreach (var p in request.Packages)
        {
            if (string.IsNullOrWhiteSpace(p.PackageName)) return OperationResult.Fail("PACKAGE_NAME_REQUIRED", "Tên quy cách đóng gói không được trống.");
            if (p.ConversionFactor <= 0) return OperationResult.Fail("INVALID_CONVERSION_FACTOR", "Hệ số quy đổi phải lớn hơn 0.");
        }

        return OperationResult.Ok();
    }

    private async Task<OperationResult> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Fail("CONFLICT", "Dữ liệu đã thay đổi, vui lòng tải lại trước khi lưu.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            return OperationResult.Fail("DUPLICATE", "Mã hoặc barcode vật tư/quy cách đóng gói đã tồn tại.");
        }
        catch (DbUpdateException)
        {
            return OperationResult.Fail("SAVE_FAILED", "Không thể lưu dữ liệu vật tư. Vui lòng kiểm tra các ràng buộc.");
        }
    }

    private static ProductDto MapToDto(Product p, Dictionary<Guid, Uom> uoms)
    {
        return MapToDto(p, null, new List<Package>(), uoms);
    }

    private static ProductDto MapToDto(Product p, ProductConfig? config, IReadOnlyList<Package> packages, Dictionary<Guid, Uom> uoms)
    {
        var configDto = config is null ? new ProductConfigDto("FULL", false, false, null, 0, 999999, "MEDIUM", "SLOW", false, 0, 0, 0, 0)
            : new ProductConfigDto(
                config.IqcCheckType,
                config.VendorInnerLotCtl,
                config.IsWafer,
                config.LotValidationRegex,
                config.MinStock,
                config.MaxStock,
                config.WeightClass,
                config.RotationSpeed,
                config.TrackSerial,
                config.Length,
                config.Width,
                config.Height,
                config.Weight
            );

        var pkgDtos = packages.Select(pkg => {
            uoms.TryGetValue(pkg.UomId, out var pkgUom);
            return new PackageDto(
                pkg.Id,
                pkg.PackageName,
                pkg.Barcode,
                pkg.UomId,
                pkgUom?.Code ?? string.Empty,
                pkgUom?.Name ?? string.Empty,
                pkg.ConversionFactor,
                pkg.IsActive,
                pkg.RowVersion
            );
        }).ToList();

        return new ProductDto(
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            p.Barcode,
            p.BaseUomId,
            p.BaseUom?.Code ?? string.Empty,
            p.BaseUom?.Name ?? string.Empty,
            p.IsActive,
            p.RowVersion,
            configDto,
            pkgDtos
        );
    }
}

