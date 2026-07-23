namespace Nexustock.Modules.Files.Entities;

/// <summary>Job chuyển file giữa storage providers (Phase 42).</summary>
public class FileStorageMigrateJob
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SourceProvider { get; set; }
    public string TargetProvider { get; set; } = "";
    public string Mode { get; set; } = "MIGRATE";
    public string Status { get; set; } = "PENDING";
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int SkipCount { get; set; }
    public int FailCount { get; set; }
    public bool DeleteSourceAfter { get; set; }
    public string? ErrorSummary { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? CursorAttachmentId { get; set; }
    /// <summary>JSON mảng Guid — snapshot ≤2000.</summary>
    public string? EligibleIdsJson { get; set; }
    public bool CancelRequested { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int EligibleFullCount { get; set; }
    public bool Truncated { get; set; }
}

public class FileStorageMigrateJobError
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid AttachmentId { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public static class MigrateJobStatuses
{
    public const string Pending = "PENDING";
    public const string Running = "RUNNING";
    public const string Paused = "PAUSED";
    public const string Completed = "COMPLETED";
    public const string CompletedWithErrors = "COMPLETED_WITH_ERRORS";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}
