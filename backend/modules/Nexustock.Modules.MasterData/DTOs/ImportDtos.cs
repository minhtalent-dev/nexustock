namespace Nexustock.Modules.MasterData.DTOs;

public sealed record ImportResultDto(
    bool Success,
    Guid BatchId,
    string ImportType,
    string Status,
    int TotalRows,
    int SuccessRows,
    int ErrorRows,
    IReadOnlyList<ImportRowErrorDto> Errors,
    string? ErrorCsvContent,
    Guid? TargetId = null,
    DateTimeOffset? ExpiresAt = null
);

public sealed record ImportRowErrorDto(
    int RowIndex,
    IReadOnlyDictionary<string, string> Raw,
    string ErrorMessage
);

public sealed record CommitImportRequest(Guid BatchId);
