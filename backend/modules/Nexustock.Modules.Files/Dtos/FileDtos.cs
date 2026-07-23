namespace Nexustock.Modules.Files.Dtos;

public record UploadResultDto(
    string FileName,
    string ContentType,
    long SizeBytes,
    string Kind,
    string Provider,
    string StorageKey,
    string Url);

public record BindAttachmentRequest(
    string EntityType,
    Guid EntityId,
    string Url,
    string Provider,
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Kind);

public record AttachmentDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Kind,
    string Provider,
    string StorageKey,
    string Url,
    DateTimeOffset CreatedAt);

public record ProviderStatusDto(string Id, string Label, bool Configured);

public record StorageSettingsDto(
    string ActiveProvider,
    string? PublicBaseUrl,
    bool LocalPathConfigured,
    IReadOnlyList<ProviderStatusDto> Providers,
    DateTimeOffset? LastTestAt,
    bool? LastTestOk,
    string? LastTestMessage);

public class UpsertStorageSettingsRequest
{
    public string? ActiveProvider { get; set; }
    public string? PublicBaseUrl { get; set; }
    public string? LocalPathOverride { get; set; }
    public Dictionary<string, string?>? Config { get; set; }
    public bool Activate { get; set; } = true;
}

public record StorageTestResultDto(bool Ok, string Message);
