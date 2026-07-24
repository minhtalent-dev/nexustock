namespace Nexustock.Modules.Files.Entities;

public class FilePendingUpload
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Kind { get; set; } = "";
    public string Provider { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string LegacyUrl { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? BoundAt { get; set; }
    public DateTimeOffset? PurgedAt { get; set; }
    public Guid? AttachmentId { get; set; }
}
