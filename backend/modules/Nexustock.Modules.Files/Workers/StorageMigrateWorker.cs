using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.Files.Services;

namespace Nexustock.Modules.Files.Workers;

/// <summary>Background worker xử lý migrate jobs — IgnoreQueryFilters + TenantId tường minh.</summary>
public sealed class StorageMigrateWorker : BackgroundService
{
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(3);
    private const long MaxBufferBytes = 10 * 1024 * 1024;

    private readonly IServiceProvider _services;
    private readonly ILogger<StorageMigrateWorker> _logger;

    public StorageMigrateWorker(IServiceProvider services, ILogger<StorageMigrateWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverStuckAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var worked = await ProcessNextAsync(stoppingToken);
                if (!worked)
                    await Task.Delay(PollDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StorageMigrate] Lỗi vòng poll");
                await Task.Delay(PollDelay, stoppingToken);
            }
        }
    }

    private async Task RecoverStuckAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var cutoff = DateTimeOffset.UtcNow - StuckThreshold;
        var stuck = await db.FileStorageMigrateJobs
            .IgnoreQueryFilters()
            .Where(j => j.Status == MigrateJobStatuses.Running
                && (j.UpdatedAt == null || j.UpdatedAt < cutoff))
            .ToListAsync(ct);

        foreach (var j in stuck)
        {
            j.Status = MigrateJobStatuses.Paused;
            j.UpdatedAt = DateTimeOffset.UtcNow;
            j.ErrorSummary = "Recovered stuck RUNNING job on worker startup";
        }
        if (stuck.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogWarning("[StorageMigrate] Recovery: {Count} job RUNNING → PAUSED", stuck.Count);
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<IObjectStorageResolver>();

        var pending = await db.FileStorageMigrateJobs
            .IgnoreQueryFilters()
            .Where(j => j.Status == MigrateJobStatuses.Pending)
            .OrderBy(j => j.CreatedAt)
            .Select(j => j.Id)
            .FirstOrDefaultAsync(ct);

        if (pending == Guid.Empty) return false;

        var claimed = await db.FileStorageMigrateJobs
            .IgnoreQueryFilters()
            .Where(j => j.Id == pending && j.Status == MigrateJobStatuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, MigrateJobStatuses.Running)
                .SetProperty(j => j.StartedAt, DateTimeOffset.UtcNow)
                .SetProperty(j => j.UpdatedAt, DateTimeOffset.UtcNow), ct);

        if (claimed != 1) return false;

        var job = await db.FileStorageMigrateJobs.IgnoreQueryFilters().FirstAsync(j => j.Id == pending, ct);
        await RunJobAsync(db, resolver, job, ct);
        return true;
    }

    private async Task RunJobAsync(
        FilesDbContext db,
        IObjectStorageResolver resolver,
        FileStorageMigrateJob job,
        CancellationToken ct)
    {
        var tenantId = job.TenantId;
        var settings = await db.FileStorageSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (settings == null)
        {
            job.Status = MigrateJobStatuses.Failed;
            job.ErrorSummary = "Settings not found for tenant";
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = job.FinishedAt;
            await db.SaveChangesAsync(ct);
            return;
        }

        IObjectStorageProvider dst;
        try { dst = resolver.ResolveByProviderId(job.TargetProvider, settings); }
        catch (Exception ex)
        {
            job.Status = MigrateJobStatuses.Failed;
            job.ErrorSummary = "Target resolve failed: " + ex.Message;
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = job.FinishedAt;
            await db.SaveChangesAsync(ct);
            return;
        }

        var ids = StorageMigrateService.ParseIds(job.EligibleIdsJson);
        var startIndex = 0;
        if (job.CursorAttachmentId.HasValue)
        {
            var idx = ids.IndexOf(job.CursorAttachmentId.Value);
            startIndex = idx >= 0 ? idx + 1 : 0;
        }

        var processedSinceSave = 0;
        for (var i = startIndex; i < ids.Count; i++)
        {
            await db.Entry(job).ReloadAsync(ct);
            if (job.CancelRequested)
            {
                job.Status = MigrateJobStatuses.Cancelled;
                job.FinishedAt = DateTimeOffset.UtcNow;
                job.UpdatedAt = job.FinishedAt;
                await db.SaveChangesAsync(ct);
                return;
            }

            var attachmentId = ids[i];
            var att = await db.FileAttachments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TenantId == tenantId, ct);

            if (att == null || att.DeletedAt != null)
            {
                job.SkipCount++;
            }
            else
            {
                try
                {
                    var result = await MigrateOneAsync(resolver, settings, dst, job, att, ct);
                    switch (result)
                    {
                        case MigrateItemResult.Success: job.SuccessCount++; break;
                        case MigrateItemResult.Skip: job.SkipCount++; break;
                    }
                }
                catch (Exception ex)
                {
                    job.FailCount++;
                    var msg = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                    db.FileStorageMigrateJobErrors.Add(new FileStorageMigrateJobError
                    {
                        Id = Guid.NewGuid(),
                        JobId = job.Id,
                        AttachmentId = attachmentId,
                        Message = msg,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    _logger.LogWarning(ex, "[StorageMigrate] Item fail job={Job} att={Att}", job.Id, attachmentId);
                }
            }

            job.CursorAttachmentId = attachmentId;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            processedSinceSave++;
            if (processedSinceSave >= 10)
            {
                await db.SaveChangesAsync(ct);
                processedSinceSave = 0;
            }
        }

        job.Status = job.FailCount > 0 ? MigrateJobStatuses.CompletedWithErrors : MigrateJobStatuses.Completed;
        job.FinishedAt = DateTimeOffset.UtcNow;
        job.UpdatedAt = job.FinishedAt;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[StorageMigrate] Job {JobId} done status={Status} ok={Ok} skip={Skip} fail={Fail}",
            job.Id, job.Status, job.SuccessCount, job.SkipCount, job.FailCount);
    }

    internal static async Task<MigrateItemResult> MigrateOneAsync(
        IObjectStorageResolver resolver,
        FileStorageSettings settings,
        IObjectStorageProvider dst,
        FileStorageMigrateJob job,
        FileAttachment att,
        CancellationToken ct)
    {
        if (string.Equals(att.Provider, job.TargetProvider, StringComparison.OrdinalIgnoreCase)
            && await dst.ExistsAsync(att.StorageKey, ct)
            && (string.IsNullOrWhiteSpace(att.ThumbnailKey) || await dst.ExistsAsync(att.ThumbnailKey, ct)))
            return MigrateItemResult.Skip;

        var sourceId = string.IsNullOrWhiteSpace(job.SourceProvider) ? att.Provider : job.SourceProvider;
        IObjectStorageProvider src;
        try { src = resolver.ResolveByProviderId(sourceId, settings); }
        catch
        {
            throw new InvalidOperationException("MIGRATE_SOURCE_CONFIG_INVALID");
        }

        // 1. Copy original file
        await using var raw = await src.OpenReadAsync(att.StorageKey, ct);
        Stream content = raw;
        MemoryStream? buffer = null;
        if (!raw.CanSeek)
        {
            buffer = new MemoryStream();
            await raw.CopyToAsync(buffer, ct);
            if (buffer.Length > MaxBufferBytes)
                throw new InvalidOperationException("MIGRATE_FILE_TOO_LARGE");
            buffer.Position = 0;
            content = buffer;
        }

        try
        {
            await dst.PutAsync(att.StorageKey, content, string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType, ct);
        }
        finally
        {
            if (buffer != null) await buffer.DisposeAsync();
        }

        if (!await dst.ExistsAsync(att.StorageKey, ct))
            throw new InvalidOperationException("MIGRATE_VERIFY_FAILED");

        // 2. Copy thumbnail file if exists
        if (!string.IsNullOrWhiteSpace(att.ThumbnailKey))
        {
            await using var thumbRaw = await src.OpenReadAsync(att.ThumbnailKey, ct);
            Stream thumbContent = thumbRaw;
            MemoryStream? thumbBuffer = null;
            if (!thumbRaw.CanSeek)
            {
                thumbBuffer = new MemoryStream();
                await thumbRaw.CopyToAsync(thumbBuffer, ct);
                thumbBuffer.Position = 0;
                thumbContent = thumbBuffer;
            }

            try
            {
                await dst.PutAsync(att.ThumbnailKey, thumbContent, "image/jpeg", ct);
            }
            finally
            {
                if (thumbBuffer != null) await thumbBuffer.DisposeAsync();
            }

            if (!await dst.ExistsAsync(att.ThumbnailKey, ct))
                throw new InvalidOperationException("MIGRATE_THUMBNAIL_VERIFY_FAILED");
        }

        att.Provider = job.TargetProvider;
        att.PublicUrl = dst.BuildPublicUrl(att.StorageKey, settings.PublicBaseUrl);
        return MigrateItemResult.Success;
    }

    internal enum MigrateItemResult { Success, Skip }
}
