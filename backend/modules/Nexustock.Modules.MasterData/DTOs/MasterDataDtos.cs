namespace Nexustock.Modules.MasterData.DTOs;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record UomDto(Guid Id, string Code, string Name, bool IsActive, int RowVersion);
public sealed record UpsertUomRequest(string Code, string Name, bool IsActive, int? RowVersion);

public sealed record WarehouseDto(Guid Id, string Code, string Name, string? Description, bool IsActive, int RowVersion);
public sealed record UpsertWarehouseRequest(string Code, string Name, string? Description, bool IsActive, int? RowVersion);

public sealed record StorageZoneDto(Guid Id, Guid WarehouseId, string WarehouseCode, string Code, string Name, string ZoneType, decimal? TemperatureLimit, bool IsLocked, int RowVersion);
public sealed record UpsertStorageZoneRequest(Guid WarehouseId, string Code, string Name, string ZoneType, decimal? TemperatureLimit, bool IsLocked, int? RowVersion);

public sealed record PartnerDto(Guid Id, string Code, string Name, string PartnerType, string? Address, string? TaxCode, bool IsActive, int RowVersion);
public sealed record UpsertPartnerRequest(string Code, string Name, string PartnerType, string? Address, string? TaxCode, bool IsActive, int? RowVersion);

public sealed record ReasonCodeDto(Guid Id, string Code, string ReasonType, string Description, bool IsActive, int RowVersion);
public sealed record UpsertReasonCodeRequest(string Code, string ReasonType, string Description, bool IsActive, int? RowVersion);

public sealed record OperationResult(bool Success, string? ErrorCode = null, string? Message = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string errorCode, string message) => new(false, errorCode, message);
}
