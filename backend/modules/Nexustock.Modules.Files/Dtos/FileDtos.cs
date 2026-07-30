namespace Nexustock.Modules.Files.Dtos;

public record UploadResultDto(
    Guid UploadId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Kind,
    string Provider,
    string Url,
    DateTimeOffset ExpiresAt);

public record BindAttachmentRequest(
    Guid? UploadId,
    string EntityType,
    Guid EntityId,
    string? Source = null);

public record AttachmentDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Kind,
    string Provider,
    string? PreviewKind,
    string ContentUrl,
    string DownloadUrl,
    string? ThumbnailUrl,
    DateTimeOffset CreatedAt);

public record AttachmentContent(
    Stream Stream,
    string ContentType,
    string FileName,
    string? ETag = null);

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
