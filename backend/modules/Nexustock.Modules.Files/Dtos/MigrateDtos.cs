namespace Nexustock.Modules.Files.Dtos;

public class MigrateDryRunRequest
{
    public string? SourceProvider { get; set; }
    public string? TargetProvider { get; set; }
}

public record MigrateDryRunDto(
    int EligibleCount,
    int AlreadyOnTarget,
    int JobTotal,
    bool Truncated,
    IReadOnlyList<string> SampleKeys,
    bool TargetTestOk,
    string? TargetProvider);

public class StartMigrateJobRequest
{
    public string? SourceProvider { get; set; }
    public string? TargetProvider { get; set; }
    public bool DeleteSourceAfter { get; set; }
}

public record MigrateJobDto(
    Guid JobId,
    string Status,
    string? SourceProvider,
    string TargetProvider,
    int TotalCount,
    int SuccessCount,
    int SkipCount,
    int FailCount,
    bool Truncated,
    int EligibleFullCount,
    bool DeleteSourceAfter,
    bool CancelRequested,
    string? ErrorSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? UpdatedAt);

public record MigrateJobErrorDto(Guid AttachmentId, string Message, DateTimeOffset CreatedAt);
