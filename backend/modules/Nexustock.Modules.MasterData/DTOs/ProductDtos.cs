namespace Nexustock.Modules.MasterData.DTOs;

public sealed record ProductConfigDto(
    string IqcCheckType,
    bool VendorInnerLotCtl,
    bool IsWafer,
    string? LotValidationRegex,
    decimal MinStock,
    decimal MaxStock,
    string WeightClass,
    string RotationSpeed,
    bool TrackSerial,
    decimal Length,
    decimal Width,
    decimal Height,
    decimal Weight
);

public sealed record PackageDto(
    Guid Id,
    string PackageName,
    string? Barcode,
    Guid UomId,
    string UomCode,
    string UomName,
    decimal ConversionFactor,
    bool IsActive,
    int RowVersion
);

public sealed record ProductDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Barcode,
    Guid BaseUomId,
    string BaseUomCode,
    string BaseUomName,
    bool IsActive,
    int RowVersion,
    ProductConfigDto Config,
    IReadOnlyList<PackageDto> Packages
);

public sealed record UpsertProductConfigRequest(
    string IqcCheckType,
    bool VendorInnerLotCtl,
    bool IsWafer,
    string? LotValidationRegex,
    decimal MinStock,
    decimal MaxStock,
    string WeightClass,
    string RotationSpeed,
    bool TrackSerial,
    decimal Length,
    decimal Width,
    decimal Height,
    decimal Weight
);

public sealed record UpsertPackageRequest(
    Guid? Id,
    string PackageName,
    string? Barcode,
    Guid UomId,
    decimal ConversionFactor,
    bool IsActive,
    int? RowVersion
);

public sealed record UpsertProductRequest(
    string Code,
    string Name,
    string? Description,
    string? Barcode,
    Guid BaseUomId,
    bool IsActive,
    int? RowVersion,
    UpsertProductConfigRequest Config,
    IReadOnlyList<UpsertPackageRequest> Packages
);
