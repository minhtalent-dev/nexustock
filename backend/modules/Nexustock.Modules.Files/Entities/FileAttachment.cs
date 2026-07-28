namespace Nexustock.Modules.Files.Entities;

public class FileAttachment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Kind { get; set; } = "";
    public string Provider { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string PublicUrl { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? PendingUploadId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? ThumbnailKey { get; set; }
    public DateTimeOffset? ObjectsPurgedAt { get; set; }
}

public class FileStorageSettings
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ActiveProvider { get; set; } = "LOCAL";
    public string? PublicBaseUrl { get; set; }
    public string? LocalPathOverride { get; set; }
    public string? ConfigJsonEncrypted { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? LastTestAt { get; set; }
    public bool? LastTestOk { get; set; }
    public string? LastTestMessage { get; set; }
}
