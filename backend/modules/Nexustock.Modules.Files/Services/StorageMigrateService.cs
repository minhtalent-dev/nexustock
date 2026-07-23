using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;

namespace Nexustock.Modules.Files.Services;

public interface IStorageMigrateService
{
    Task<MigrateDryRunDto> DryRunAsync(MigrateDryRunRequest request, CancellationToken ct);
    Task<MigrateJobDto> StartAsync(StartMigrateJobRequest request, string? user, CancellationToken ct);
    Task<MigrateJobDto?> GetAsync(Guid jobId, CancellationToken ct);
    Task<MigrateJobDto?> GetActiveAsync(CancellationToken ct);
    Task<MigrateJobDto> CancelAsync(Guid jobId, CancellationToken ct);
    Task<MigrateJobDto> ResumeAsync(Guid jobId, CancellationToken ct);
    Task<MigrateJobDto> PurgeSourceAsync(Guid jobId, CancellationToken ct);
    Task<IReadOnlyList<MigrateJobErrorDto>> GetErrorsAsync(Guid jobId, int take, CancellationToken ct);
}

public sealed class StorageMigrateService : IStorageMigrateService
{
    public const int CapPerJob = 2000;
    private static readonly TimeSpan TestFreshness = TimeSpan.FromHours(24);

    private readonly FilesDbContext _db;
    private readonly FileStorageService _storage;
    private readonly IObjectStorageResolver _resolver;
    private readonly IFileStorageSettingsService _settingsService;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StorageMigrateService> _logger;

    public StorageMigrateService(
        FilesDbContext db,
        FileStorageService storage,
        IObjectStorageResolver resolver,
        IFileStorageSettingsService settingsService,
        IHostEnvironment env,
        IConfiguration configuration,
        ILogger<StorageMigrateService> logger)
    {
        _db = db;
        _storage = storage;
        _resolver = resolver;
        _settingsService = settingsService;
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MigrateDryRunDto> DryRunAsync(MigrateDryRunRequest request, CancellationToken ct)
    {
        var settings = await _storage.GetOrCreateSettingsAsync(ct);
        var target = ResolveTarget(request.TargetProvider, settings);
        EnsureFakeAllowed(target);
        var source = NormalizeSource(request.SourceProvider);
        EnsureSourceNotEqualsTarget(source, target);

        var q = EligibleQuery(settings.TenantId, source, target);
        var eligibleFull = await q.CountAsync(ct);
        var already = await _db.FileAttachments.AsNoTracking()
            .Where(a => a.DeletedAt == null && a.Provider == target)
            .CountAsync(ct);

        var sample = await q.OrderBy(a => a.Id).Take(20).Select(a => a.StorageKey).ToListAsync(ct);
        var jobTotal = Math.Min(eligibleFull, CapPerJob);
        var testOk = IsTargetTestFresh(settings);

        return new MigrateDryRunDto(
            eligibleFull,
            already,
            jobTotal,
            eligibleFull > CapPerJob,
            sample,
            testOk,
            target);
    }

    public async Task<MigrateJobDto> StartAsync(StartMigrateJobRequest request, string? user, CancellationToken ct)
    {
        var settings = await _storage.GetOrCreateSettingsAsync(ct);
        var target = ResolveTarget(request.TargetProvider, settings);
        EnsureFakeAllowed(target);
        EnsureTargetActive(target, settings);
        var source = NormalizeSource(request.SourceProvider);
        EnsureSourceNotEqualsTarget(source, target);

        if (!IsTargetTestFresh(settings))
        {
            // Inline re-test trước khi start
            try
            {
                await _settingsService.TestAsync(new UpsertStorageSettingsRequest
                {
                    ActiveProvider = target,
                    Activate = false
                }, ct);
                settings = await _storage.GetOrCreateSettingsAsync(ct);
            }
            catch (FileDomainException)
            {
                throw new FileDomainException("MIGRATE_TARGET_TEST_REQUIRED", "Target storage test required or failed");
            }
            if (!IsTargetTestFresh(settings))
                throw new FileDomainException("MIGRATE_TARGET_TEST_REQUIRED", "Target storage test required or stale (>24h)");
        }

        var tenantId = settings.TenantId;
        var inProgress = await _db.FileStorageMigrateJobs
            .AnyAsync(j => j.Status == MigrateJobStatuses.Pending || j.Status == MigrateJobStatuses.Running, ct);
        if (inProgress)
            throw new FileDomainException("MIGRATE_JOB_IN_PROGRESS", "A migrate job is already in progress", 409);

        var q = EligibleQuery(tenantId, source, target);
        var eligibleFull = await q.CountAsync(ct);
        var ids = await q.OrderBy(a => a.Id).Take(CapPerJob).Select(a => a.Id).ToListAsync(ct);
        if (ids.Count == 0)
            throw new FileDomainException("MIGRATE_NOTHING_TO_DO", "No eligible attachments to migrate");

        if (source != null)
        {
            try { _ = _resolver.ResolveByProviderId(source, settings); }
            catch
            {
                throw new FileDomainException("MIGRATE_SOURCE_CONFIG_INVALID", "Cannot resolve source provider credentials");
            }
        }

        try { _ = _resolver.ResolveByProviderId(target, settings); }
        catch
        {
            throw new FileDomainException("MIGRATE_SOURCE_CONFIG_INVALID", "Cannot resolve target provider credentials");
        }

        var now = DateTimeOffset.UtcNow;
        var job = new FileStorageMigrateJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceProvider = source,
            TargetProvider = target,
            Mode = "MIGRATE",
            Status = MigrateJobStatuses.Pending,
            TotalCount = ids.Count,
            EligibleFullCount = eligibleFull,
            Truncated = eligibleFull > CapPerJob,
            DeleteSourceAfter = request.DeleteSourceAfter,
            EligibleIdsJson = JsonSerializer.Serialize(ids),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = user,
            CancelRequested = false
        };
        _db.FileStorageMigrateJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Migrate job {JobId} created tenant={Tenant} {Count} files {Src}→{Dst}",
            job.Id, tenantId, ids.Count, source ?? "*", target);
        return ToDto(job);
    }

    public async Task<MigrateJobDto?> GetAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.FileStorageMigrateJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        return job == null ? null : ToDto(job);
    }

    public async Task<MigrateJobDto?> GetActiveAsync(CancellationToken ct)
    {
        var job = await _db.FileStorageMigrateJobs.AsNoTracking()
            .Where(j => j.Status == MigrateJobStatuses.Pending
                || j.Status == MigrateJobStatuses.Running
                || j.Status == MigrateJobStatuses.Paused)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);
        return job == null ? null : ToDto(job);
    }

    public async Task<MigrateJobDto> CancelAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.FileStorageMigrateJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)
            ?? throw new FileDomainException("MIGRATE_JOB_NOT_FOUND", "Job not found", 404);

        if (job.Status is MigrateJobStatuses.Completed or MigrateJobStatuses.CompletedWithErrors or MigrateJobStatuses.Cancelled)
            return ToDto(job);

        job.CancelRequested = true;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        if (job.Status is MigrateJobStatuses.Pending or MigrateJobStatuses.Paused)
        {
            job.Status = MigrateJobStatuses.Cancelled;
            job.FinishedAt = job.UpdatedAt;
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(job);
    }

    public async Task<MigrateJobDto> ResumeAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.FileStorageMigrateJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)
            ?? throw new FileDomainException("MIGRATE_JOB_NOT_FOUND", "Job not found", 404);

        if (job.Status is not (MigrateJobStatuses.Paused or MigrateJobStatuses.Failed or MigrateJobStatuses.Cancelled))
            throw new FileDomainException("MIGRATE_RESUME_INVALID", "Job cannot be resumed from current status");

        var inProgress = await _db.FileStorageMigrateJobs
            .AnyAsync(j => j.Id != jobId && (j.Status == MigrateJobStatuses.Pending || j.Status == MigrateJobStatuses.Running), ct);
        if (inProgress)
            throw new FileDomainException("MIGRATE_JOB_IN_PROGRESS", "Another migrate job is in progress", 409);

        job.Status = MigrateJobStatuses.Pending;
        job.CancelRequested = false;
        job.FinishedAt = null;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(job);
    }

    public async Task<MigrateJobDto> PurgeSourceAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.FileStorageMigrateJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)
            ?? throw new FileDomainException("MIGRATE_JOB_NOT_FOUND", "Job not found", 404);

        if (job.Status is not (MigrateJobStatuses.Completed or MigrateJobStatuses.CompletedWithErrors))
            throw new FileDomainException("MIGRATE_NOT_COMPLETED", "Purge only allowed after completed migrate");

        var settings = await _db.FileStorageSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == job.TenantId, ct)
            ?? throw new FileDomainException("STORAGE_CONFIG_INVALID", "Settings not found");

        var ids = ParseIds(job.EligibleIdsJson);
        var attachments = await _db.FileAttachments
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == job.TenantId && ids.Contains(a.Id) && a.Provider == job.TargetProvider)
            .ToListAsync(ct);

        // Source provider: job.SourceProvider hoặc suy từ trước khi migrate — dùng SourceProvider; nếu null thì skip purge multi
        if (string.IsNullOrWhiteSpace(job.SourceProvider))
            throw new FileDomainException("MIGRATE_SOURCE_CONFIG_INVALID", "Purge requires a specific sourceProvider on the job");

        IObjectStorageProvider src;
        try { src = _resolver.ResolveByProviderId(job.SourceProvider, settings); }
        catch
        {
            throw new FileDomainException("MIGRATE_SOURCE_CONFIG_INVALID", "Cannot resolve source for purge");
        }

        foreach (var att in attachments)
        {
            try
            {
                // Chỉ xóa trên source nếu object còn (key giữ nguyên trên target)
                if (await src.ExistsAsync(att.StorageKey, ct))
                    await src.DeleteAsync(att.StorageKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Purge source failed for {Key}", att.StorageKey);
            }
        }

        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(job);
    }

    public async Task<IReadOnlyList<MigrateJobErrorDto>> GetErrorsAsync(Guid jobId, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 200);
        return await _db.FileStorageMigrateJobErrors.AsNoTracking()
            .Where(e => e.JobId == jobId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new MigrateJobErrorDto(e.AttachmentId, e.Message, e.CreatedAt))
            .ToListAsync(ct);
    }

    private IQueryable<FileAttachment> EligibleQuery(Guid tenantId, string? source, string target)
    {
        // HTTP scope đã filter tenant; vẫn tường minh
        var q = _db.FileAttachments.AsNoTracking()
            .Where(a => a.DeletedAt == null && a.Provider != target);

        if (!string.IsNullOrWhiteSpace(source))
            q = q.Where(a => a.Provider == source);

        return q;
    }

    private static string ResolveTarget(string? requested, FileStorageSettings settings)
    {
        var target = string.IsNullOrWhiteSpace(requested)
            ? settings.ActiveProvider
            : requested.Trim().ToUpperInvariant();
        return target;
    }

    private void EnsureTargetActive(string target, FileStorageSettings settings)
    {
        if (!string.Equals(target, settings.ActiveProvider, StringComparison.OrdinalIgnoreCase))
            throw new FileDomainException("MIGRATE_TARGET_NOT_ACTIVE", "Target must equal active storage provider");
    }

    private void EnsureFakeAllowed(string provider)
    {
        if (!string.Equals(provider, StorageProviderIds.Fake, StringComparison.OrdinalIgnoreCase))
            return;
        var allow = _configuration.GetValue("Migrate:AllowFake", false);
        if (!_env.IsDevelopment() && !allow)
            throw new FileDomainException("MIGRATE_FAKE_FORBIDDEN", "FAKE provider is not allowed outside Development");
    }

    private static string? NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            || source.Equals("*", StringComparison.OrdinalIgnoreCase))
            return null;
        return source.Trim().ToUpperInvariant();
    }

    private static void EnsureSourceNotEqualsTarget(string? source, string target)
    {
        if (source != null && string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            throw new FileDomainException("MIGRATE_SOURCE_EQUALS_TARGET", "Source and target providers must differ");
    }

    private static bool IsTargetTestFresh(FileStorageSettings settings)
    {
        var active = settings.ActiveProvider;
        if (string.Equals(active, StorageProviderIds.Local, StringComparison.OrdinalIgnoreCase)
            || string.Equals(active, StorageProviderIds.Fake, StringComparison.OrdinalIgnoreCase))
            return true;
        return settings.LastTestOk == true
            && settings.LastTestAt.HasValue
            && settings.LastTestAt.Value >= DateTimeOffset.UtcNow - TestFreshness;
    }

    internal static List<Guid> ParseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static MigrateJobDto ToDto(FileStorageMigrateJob job) => new(
        job.Id,
        job.Status,
        job.SourceProvider,
        job.TargetProvider,
        job.TotalCount,
        job.SuccessCount,
        job.SkipCount,
        job.FailCount,
        job.Truncated,
        job.EligibleFullCount,
        job.DeleteSourceAfter,
        job.CancelRequested,
        job.ErrorSummary,
        job.CreatedAt,
        job.StartedAt,
        job.FinishedAt,
        job.UpdatedAt);
}
