using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Entities;

namespace Nexustock.Modules.MasterData.Services;

public interface ILookupMasterDataService
{
    Task<PagedResult<UomDto>> GetUomsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<UomDto?> GetUomAsync(Guid id, CancellationToken cancellationToken);
    Task<(OperationResult Result, UomDto? Item)> CreateUomAsync(UpsertUomRequest request, CancellationToken cancellationToken);
    Task<(OperationResult Result, UomDto? Item)> UpdateUomAsync(Guid id, UpsertUomRequest request, CancellationToken cancellationToken);
    Task<OperationResult> DeleteUomAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<WarehouseDto>> GetWarehousesAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<WarehouseDto?> GetWarehouseAsync(Guid id, CancellationToken cancellationToken);
    Task<(OperationResult Result, WarehouseDto? Item)> CreateWarehouseAsync(UpsertWarehouseRequest request, CancellationToken cancellationToken);
    Task<(OperationResult Result, WarehouseDto? Item)> UpdateWarehouseAsync(Guid id, UpsertWarehouseRequest request, CancellationToken cancellationToken);
    Task<OperationResult> DeleteWarehouseAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<StorageZoneDto>> GetZonesAsync(string? search, Guid? warehouseId, int page, int pageSize, CancellationToken cancellationToken);
    Task<StorageZoneDto?> GetZoneAsync(Guid id, CancellationToken cancellationToken);
    Task<(OperationResult Result, StorageZoneDto? Item)> CreateZoneAsync(UpsertStorageZoneRequest request, CancellationToken cancellationToken);
    Task<(OperationResult Result, StorageZoneDto? Item)> UpdateZoneAsync(Guid id, UpsertStorageZoneRequest request, CancellationToken cancellationToken);
    Task<OperationResult> DeleteZoneAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<PartnerDto>> GetPartnersAsync(string? search, string? partnerType, int page, int pageSize, CancellationToken cancellationToken);
    Task<PartnerDto?> GetPartnerAsync(Guid id, CancellationToken cancellationToken);
    Task<(OperationResult Result, PartnerDto? Item)> CreatePartnerAsync(UpsertPartnerRequest request, CancellationToken cancellationToken);
    Task<(OperationResult Result, PartnerDto? Item)> UpdatePartnerAsync(Guid id, UpsertPartnerRequest request, CancellationToken cancellationToken);
    Task<OperationResult> DeletePartnerAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ReasonCodeDto>> GetReasonCodesAsync(string? search, string? reasonType, int page, int pageSize, CancellationToken cancellationToken);
    Task<ReasonCodeDto?> GetReasonCodeAsync(Guid id, CancellationToken cancellationToken);
    Task<(OperationResult Result, ReasonCodeDto? Item)> CreateReasonCodeAsync(UpsertReasonCodeRequest request, CancellationToken cancellationToken);
    Task<(OperationResult Result, ReasonCodeDto? Item)> UpdateReasonCodeAsync(Guid id, UpsertReasonCodeRequest request, CancellationToken cancellationToken);
    Task<OperationResult> DeleteReasonCodeAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<StorageLocationDto>> GetLocationsAsync(string? search, Guid? zoneId, bool? isLocked, int page, int pageSize, CancellationToken cancellationToken);
    Task<StorageLocationDto?> GetLocationAsync(Guid id, CancellationToken cancellationToken);
    Task<(OperationResult Result, StorageLocationDto? Item)> CreateLocationAsync(UpsertStorageLocationRequest request, CancellationToken cancellationToken);
    Task<(OperationResult Result, StorageLocationDto? Item)> UpdateLocationAsync(Guid id, UpsertStorageLocationRequest request, CancellationToken cancellationToken);
    Task<OperationResult> DeleteLocationAsync(Guid id, CancellationToken cancellationToken);
}

public class LookupMasterDataService : ILookupMasterDataService
{
    private readonly MasterDataDbContext _db;

    public LookupMasterDataService(MasterDataDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<UomDto>> GetUomsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Uoms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(keyword) || x.Name.ToUpper().Contains(keyword));
        }

        return await ToPagedResultAsync(query.OrderBy(x => x.Code).Select(x => ToDto(x)), page, pageSize, cancellationToken);
    }

    public Task<UomDto?> GetUomAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Uoms.AsNoTracking().Where(x => x.Id == id).Select(x => ToDto(x)).FirstOrDefaultAsync(cancellationToken);

    public async Task<(OperationResult Result, UomDto? Item)> CreateUomAsync(UpsertUomRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateCodeName(request.Code, request.Name);
        if (!validation.Success) return (validation, null);

        var entity = new Uom { Id = Guid.NewGuid(), TenantId = _db.CurrentTenantId, Code = NormalizeCode(request.Code), Name = request.Name.Trim(), IsActive = request.IsActive };
        _db.Uoms.Add(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, ToDto(entity)) : (result, null);
    }

    public async Task<(OperationResult Result, UomDto? Item)> UpdateUomAsync(Guid id, UpsertUomRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.Uoms.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return (OperationResult.Fail("NOT_FOUND", "Không tìm thấy đơn vị tính."), null);
        var validation = ValidateVersionAndCodeName(entity.RowVersion, request.RowVersion, request.Code, request.Name);
        if (!validation.Success) return (validation, null);
        entity.Code = NormalizeCode(request.Code); entity.Name = request.Name.Trim(); entity.IsActive = request.IsActive; Touch(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, ToDto(entity)) : (result, null);
    }

    public async Task<OperationResult> DeleteUomAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Uoms.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return OperationResult.Fail("NOT_FOUND", "Không tìm thấy đơn vị tính.");
        _db.Uoms.Remove(entity);
        return await SaveAsync(cancellationToken);
    }

    public async Task<PagedResult<WarehouseDto>> GetWarehousesAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Warehouses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(keyword) || x.Name.ToUpper().Contains(keyword));
        }
        return await ToPagedResultAsync(query.OrderBy(x => x.Code).Select(x => ToDto(x)), page, pageSize, cancellationToken);
    }

    public Task<WarehouseDto?> GetWarehouseAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Warehouses.AsNoTracking().Where(x => x.Id == id).Select(x => ToDto(x)).FirstOrDefaultAsync(cancellationToken);

    public async Task<(OperationResult Result, WarehouseDto? Item)> CreateWarehouseAsync(UpsertWarehouseRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateCodeName(request.Code, request.Name);
        if (!validation.Success) return (validation, null);
        var entity = new Warehouse { Id = Guid.NewGuid(), TenantId = _db.CurrentTenantId, Code = NormalizeCode(request.Code), Name = request.Name.Trim(), Description = TrimOrNull(request.Description), IsActive = request.IsActive };
        _db.Warehouses.Add(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, ToDto(entity)) : (result, null);
    }

    public async Task<(OperationResult Result, WarehouseDto? Item)> UpdateWarehouseAsync(Guid id, UpsertWarehouseRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return (OperationResult.Fail("NOT_FOUND", "Không tìm thấy kho."), null);
        var validation = ValidateVersionAndCodeName(entity.RowVersion, request.RowVersion, request.Code, request.Name);
        if (!validation.Success) return (validation, null);
        entity.Code = NormalizeCode(request.Code); entity.Name = request.Name.Trim(); entity.Description = TrimOrNull(request.Description); entity.IsActive = request.IsActive; Touch(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, ToDto(entity)) : (result, null);
    }

    public async Task<OperationResult> DeleteWarehouseAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return OperationResult.Fail("NOT_FOUND", "Không tìm thấy kho.");
        _db.Warehouses.Remove(entity);
        return await SaveAsync(cancellationToken);
    }

    public async Task<PagedResult<StorageZoneDto>> GetZonesAsync(string? search, Guid? warehouseId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.StorageZones.AsNoTracking().Include(x => x.Warehouse).AsQueryable();
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(keyword) || x.Name.ToUpper().Contains(keyword));
        }
        return await ToPagedResultAsync(query.OrderBy(x => x.Code).Select(x => ToDto(x)), page, pageSize, cancellationToken);
    }

    public Task<StorageZoneDto?> GetZoneAsync(Guid id, CancellationToken cancellationToken) =>
        _db.StorageZones.AsNoTracking().Include(x => x.Warehouse).Where(x => x.Id == id).Select(x => ToDto(x)).FirstOrDefaultAsync(cancellationToken);

    public async Task<(OperationResult Result, StorageZoneDto? Item)> CreateZoneAsync(UpsertStorageZoneRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateCodeName(request.Code, request.Name);
        if (!validation.Success) return (validation, null);
        if (!await _db.Warehouses.AnyAsync(x => x.Id == request.WarehouseId, cancellationToken)) return (OperationResult.Fail("WAREHOUSE_NOT_FOUND", "Không tìm thấy kho."), null);
        var entity = new StorageZone { Id = Guid.NewGuid(), TenantId = _db.CurrentTenantId, WarehouseId = request.WarehouseId, Code = NormalizeCode(request.Code), Name = request.Name.Trim(), ZoneType = NormalizeCode(request.ZoneType), TemperatureLimit = request.TemperatureLimit, IsLocked = request.IsLocked };
        _db.StorageZones.Add(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, await GetZoneAsync(entity.Id, cancellationToken)) : (result, null);
    }

    public async Task<(OperationResult Result, StorageZoneDto? Item)> UpdateZoneAsync(Guid id, UpsertStorageZoneRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.StorageZones.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return (OperationResult.Fail("NOT_FOUND", "Không tìm thấy khu vực."), null);
        var validation = ValidateVersionAndCodeName(entity.RowVersion, request.RowVersion, request.Code, request.Name);
        if (!validation.Success) return (validation, null);
        if (!await _db.Warehouses.AnyAsync(x => x.Id == request.WarehouseId, cancellationToken)) return (OperationResult.Fail("WAREHOUSE_NOT_FOUND", "Không tìm thấy kho."), null);
        entity.WarehouseId = request.WarehouseId; entity.Code = NormalizeCode(request.Code); entity.Name = request.Name.Trim(); entity.ZoneType = NormalizeCode(request.ZoneType); entity.TemperatureLimit = request.TemperatureLimit; entity.IsLocked = request.IsLocked; Touch(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, await GetZoneAsync(entity.Id, cancellationToken)) : (result, null);
    }

    public async Task<OperationResult> DeleteZoneAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.StorageZones.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return OperationResult.Fail("NOT_FOUND", "Không tìm thấy khu vực.");
        _db.StorageZones.Remove(entity);
        return await SaveAsync(cancellationToken);
    }

    public async Task<PagedResult<PartnerDto>> GetPartnersAsync(string? search, string? partnerType, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Partners.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(partnerType)) query = query.Where(x => x.PartnerType == NormalizeCode(partnerType));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(keyword) || x.Name.ToUpper().Contains(keyword));
        }
        return await ToPagedResultAsync(query.OrderBy(x => x.Code).Select(x => ToDto(x)), page, pageSize, cancellationToken);
    }

    public Task<PartnerDto?> GetPartnerAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Partners.AsNoTracking().Where(x => x.Id == id).Select(x => ToDto(x)).FirstOrDefaultAsync(cancellationToken);

    public async Task<(OperationResult Result, PartnerDto? Item)> CreatePartnerAsync(UpsertPartnerRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateCodeName(request.Code, request.Name);
        if (!validation.Success) return (validation, null);
        var entity = new Partner { Id = Guid.NewGuid(), TenantId = _db.CurrentTenantId, Code = NormalizeCode(request.Code), Name = request.Name.Trim(), PartnerType = NormalizeCode(request.PartnerType), Address = TrimOrNull(request.Address), TaxCode = TrimOrNull(request.TaxCode), IsActive = request.IsActive };
        _db.Partners.Add(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, ToDto(entity)) : (result, null);
    }

    public async Task<(OperationResult Result, PartnerDto? Item)> UpdatePartnerAsync(Guid id, UpsertPartnerRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return (OperationResult.Fail("NOT_FOUND", "Không tìm thấy đối tác."), null);
        var validation = ValidateVersionAndCodeName(entity.RowVersion, request.RowVersion, request.Code, request.Name);
        if (!validation.Success) return (validation, null);
        entity.Code = NormalizeCode(request.Code); entity.Name = request.Name.Trim(); entity.PartnerType = NormalizeCode(request.PartnerType); entity.Address = TrimOrNull(request.Address); entity.TaxCode = TrimOrNull(request.TaxCode); entity.IsActive = request.IsActive; Touch(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, ToDto(entity)) : (result, null);
    }

    public async Task<OperationResult> DeletePartnerAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return OperationResult.Fail("NOT_FOUND", "Không tìm thấy đối tác.");
        _db.Partners.Remove(entity);
        return await SaveAsync(cancellationToken);
    }

    public async Task<PagedResult<ReasonCodeDto>> GetReasonCodesAsync(string? search, string? reasonType, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.ReasonCodes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(reasonType)) query = query.Where(x => x.ReasonType == NormalizeCode(reasonType));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(keyword) || x.Description.ToUpper().Contains(keyword));
        }
        return await ToPagedResultAsync(query.OrderBy(x => x.ReasonType).ThenBy(x => x.Code).Select(x => ToDto(x)), page, pageSize, cancellationToken);
    }

    public Task<ReasonCodeDto?> GetReasonCodeAsync(Guid id, CancellationToken cancellationToken) =>
        _db.ReasonCodes.AsNoTracking().Where(x => x.Id == id).Select(x => ToDto(x)).FirstOrDefaultAsync(cancellationToken);

    public async Task<(OperationResult Result, ReasonCodeDto? Item)> CreateReasonCodeAsync(UpsertReasonCodeRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateCodeName(request.Code, request.Description);
        if (!validation.Success) return (validation, null);
        var entity = new ReasonCode { Id = Guid.NewGuid(), TenantId = _db.CurrentTenantId, Code = NormalizeCode(request.Code), ReasonType = NormalizeCode(request.ReasonType), Description = request.Description.Trim(), IsActive = request.IsActive };
        _db.ReasonCodes.Add(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, ToDto(entity)) : (result, null);
    }

    public async Task<(OperationResult Result, ReasonCodeDto? Item)> UpdateReasonCodeAsync(Guid id, UpsertReasonCodeRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.ReasonCodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return (OperationResult.Fail("NOT_FOUND", "Không tìm thấy lý do."), null);
        var validation = ValidateVersionAndCodeName(entity.RowVersion, request.RowVersion, request.Code, request.Description);
        if (!validation.Success) return (validation, null);
        entity.Code = NormalizeCode(request.Code); entity.ReasonType = NormalizeCode(request.ReasonType); entity.Description = request.Description.Trim(); entity.IsActive = request.IsActive; Touch(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, ToDto(entity)) : (result, null);
    }

    public async Task<OperationResult> DeleteReasonCodeAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.ReasonCodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return OperationResult.Fail("NOT_FOUND", "Không tìm thấy lý do.");
        _db.ReasonCodes.Remove(entity);
        return await SaveAsync(cancellationToken);
    }

    private static async Task<PagedResult<T>> ToPagedResultAsync<T>(IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, total, page, pageSize);
    }

    private static OperationResult ValidateVersionAndCodeName(int currentVersion, int? requestVersion, string code, string name)
    {
        if (requestVersion is null || requestVersion.Value != currentVersion)
        {
            return OperationResult.Fail("CONFLICT", "Dữ liệu đã thay đổi, vui lòng tải lại trước khi lưu.");
        }
        return ValidateCodeName(code, name);
    }

    private static OperationResult ValidateCodeName(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code)) return OperationResult.Fail("CODE_REQUIRED", "Mã không được để trống.");
        if (string.IsNullOrWhiteSpace(name)) return OperationResult.Fail("NAME_REQUIRED", "Tên không được để trống.");
        return OperationResult.Ok();
    }

    private async Task<OperationResult> SaveAsync(CancellationToken cancellationToken)
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
            return OperationResult.Fail("DUPLICATE", "Mã hoặc barcode đã tồn tại.");
        }
        catch (DbUpdateException)
        {
            return OperationResult.Fail("SAVE_FAILED", "Không thể lưu dữ liệu. Vui lòng kiểm tra ràng buộc liên quan.");
        }
    }

    public async Task<PagedResult<StorageLocationDto>> GetLocationsAsync(string? search, Guid? zoneId, bool? isLocked, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.StorageLocations.AsNoTracking().Include(x => x.Zone).ThenInclude(x => x!.Warehouse).AsQueryable();
        if (zoneId.HasValue) query = query.Where(x => x.ZoneId == zoneId.Value);
        if (isLocked.HasValue) query = query.Where(x => x.IsLocked == isLocked.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(keyword));
        }
        return await ToPagedResultAsync(query.OrderBy(x => x.Code).Select(x => ToDto(x)), page, pageSize, cancellationToken);
    }

    public Task<StorageLocationDto?> GetLocationAsync(Guid id, CancellationToken cancellationToken) =>
        _db.StorageLocations.AsNoTracking().Include(x => x.Zone).ThenInclude(x => x!.Warehouse).Where(x => x.Id == id).Select(x => ToDto(x)).FirstOrDefaultAsync(cancellationToken);

    public async Task<(OperationResult Result, StorageLocationDto? Item)> CreateLocationAsync(UpsertStorageLocationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return (OperationResult.Fail("CODE_REQUIRED", "Mã không được để trống."), null);
        if (!await _db.StorageZones.AnyAsync(x => x.Id == request.ZoneId, cancellationToken)) return (OperationResult.Fail("ZONE_NOT_FOUND", "Không tìm thấy vùng kho."), null);
        var entity = new StorageLocation
        {
            Id = Guid.NewGuid(),
            TenantId = _db.CurrentTenantId,
            ZoneId = request.ZoneId,
            Code = NormalizeCode(request.Code),
            MaxCapacity = request.MaxCapacity,
            MaxVolume = request.MaxVolume,
            XCoord = request.XCoord,
            YCoord = request.YCoord,
            ZCoord = request.ZCoord,
            Length = request.Length,
            Width = request.Width,
            Height = request.Height,
            IsLocked = request.IsLocked,
            LockReasonCode = TrimOrNull(request.LockReasonCode),
            IsActive = request.IsActive
        };
        _db.StorageLocations.Add(entity);
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, await GetLocationAsync(entity.Id, cancellationToken)) : (result, null);
    }

    public async Task<(OperationResult Result, StorageLocationDto? Item)> UpdateLocationAsync(Guid id, UpsertStorageLocationRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.StorageLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return (OperationResult.Fail("NOT_FOUND", "Không tìm thấy vị trí."), null);
        if (request.RowVersion is null || request.RowVersion.Value != entity.RowVersion) return (OperationResult.Fail("CONFLICT", "Dữ liệu đã thay đổi, vui lòng tải lại trước khi lưu."), null);
        if (string.IsNullOrWhiteSpace(request.Code)) return (OperationResult.Fail("CODE_REQUIRED", "Mã không được để trống."), null);
        if (!await _db.StorageZones.AnyAsync(x => x.Id == request.ZoneId, cancellationToken)) return (OperationResult.Fail("ZONE_NOT_FOUND", "Không tìm thấy vùng kho."), null);
        
        entity.ZoneId = request.ZoneId;
        entity.Code = NormalizeCode(request.Code);
        entity.MaxCapacity = request.MaxCapacity;
        entity.MaxVolume = request.MaxVolume;
        entity.XCoord = request.XCoord;
        entity.YCoord = request.YCoord;
        entity.ZCoord = request.ZCoord;
        entity.Length = request.Length;
        entity.Width = request.Width;
        entity.Height = request.Height;
        entity.IsLocked = request.IsLocked;
        entity.LockReasonCode = TrimOrNull(request.LockReasonCode);
        entity.IsActive = request.IsActive;
        Touch(entity);
        
        var result = await SaveAsync(cancellationToken);
        return result.Success ? (result, await GetLocationAsync(entity.Id, cancellationToken)) : (result, null);
    }

    public async Task<OperationResult> DeleteLocationAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.StorageLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return OperationResult.Fail("NOT_FOUND", "Không tìm thấy vị trí.");
        _db.StorageLocations.Remove(entity);
        return await SaveAsync(cancellationToken);
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Touch(Uom entity) { entity.UpdatedAt = DateTimeOffset.UtcNow; entity.RowVersion++; }
    private static void Touch(Warehouse entity) { entity.UpdatedAt = DateTimeOffset.UtcNow; entity.RowVersion++; }
    private static void Touch(StorageZone entity) { entity.UpdatedAt = DateTimeOffset.UtcNow; entity.RowVersion++; }
    private static void Touch(Partner entity) { entity.UpdatedAt = DateTimeOffset.UtcNow; entity.RowVersion++; }
    private static void Touch(ReasonCode entity) { entity.UpdatedAt = DateTimeOffset.UtcNow; entity.RowVersion++; }
    private static void Touch(StorageLocation entity) { entity.UpdatedAt = DateTimeOffset.UtcNow; entity.RowVersion++; }

    private static UomDto ToDto(Uom x) => new(x.Id, x.Code, x.Name, x.IsActive, x.RowVersion);
    private static WarehouseDto ToDto(Warehouse x) => new(x.Id, x.Code, x.Name, x.Description, x.IsActive, x.RowVersion);
    private static StorageZoneDto ToDto(StorageZone x) => new(x.Id, x.WarehouseId, x.Warehouse == null ? string.Empty : x.Warehouse.Code, x.Code, x.Name, x.ZoneType, x.TemperatureLimit, x.IsLocked, x.RowVersion);
    private static PartnerDto ToDto(Partner x) => new(x.Id, x.Code, x.Name, x.PartnerType, x.Address, x.TaxCode, x.IsActive, x.RowVersion);
    private static ReasonCodeDto ToDto(ReasonCode x) => new(x.Id, x.Code, x.ReasonType, x.Description, x.IsActive, x.RowVersion);
    private static StorageLocationDto ToDto(StorageLocation x) => new(x.Id, x.ZoneId, x.Zone == null ? string.Empty : x.Zone.Code, x.Zone == null ? string.Empty : x.Zone.Name, x.Zone?.Warehouse?.Code ?? string.Empty, x.Code, x.MaxCapacity, x.MaxVolume, x.XCoord, x.YCoord, x.ZCoord, x.Length, x.Width, x.Height, x.IsLocked, x.LockReasonCode, x.IsActive, x.RowVersion);
}
