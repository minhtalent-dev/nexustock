namespace Nexustock.Modules.MasterData.DTOs;

public sealed record StorageLocationDto(
    Guid Id,
    Guid ZoneId,
    string ZoneCode,
    string ZoneName,
    string WarehouseCode,
    string Code,
    decimal MaxCapacity,
    decimal MaxVolume,
    int XCoord,
    int YCoord,
    int ZCoord,
    decimal Length,
    decimal Width,
    decimal Height,
    bool IsLocked,
    string? LockReasonCode,
    bool IsActive,
    int RowVersion
);

public sealed record UpsertStorageLocationRequest(
    Guid ZoneId,
    string Code,
    decimal MaxCapacity,
    decimal MaxVolume,
    int XCoord,
    int YCoord,
    int ZCoord,
    decimal Length,
    decimal Width,
    decimal Height,
    bool IsLocked,
    string? LockReasonCode,
    bool IsActive,
    int? RowVersion
);
