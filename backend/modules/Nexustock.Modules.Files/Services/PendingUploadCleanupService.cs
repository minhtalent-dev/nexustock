using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Files.Contexts;

namespace Nexustock.Modules.Files.Services;

public interface IPendingUploadCleanupService
{
    Task<int> CleanupExpiredPendingUploadsAsync(CancellationToken ct);
}

public sealed class PendingUploadCleanupService : IPendingUploadCleanupService
{
    private readonly FilesDbContext _db;
    private readonly FileStorageService _storage;
    private readonly IObjectStorageResolver _resolver;
    private readonly ILogger<PendingUploadCleanupService> _logger;

    public PendingUploadCleanupService(
        FilesDbContext db,
        FileStorageService storage,
        IObjectStorageResolver resolver,
        ILogger<PendingUploadCleanupService> logger)
    {
        _db = db;
        _storage = storage;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<int> CleanupExpiredPendingUploadsAsync(CancellationToken ct)
    {
        var expiredRows = await _db.FilePendingUploads
            .Where(p => p.Status == "PENDING" && p.ExpiresAt <= DateTimeOffset.UtcNow)
            .OrderBy(p => p.ExpiresAt)
            .Take(100)
            .ToListAsync(ct);

        if (expiredRows.Count == 0) return 0;

        int cleanedCount = 0;
        var settings = await _storage.GetOrCreateSettingsAsync(ct);

        foreach (var row in expiredRows)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var provider = _resolver.ResolveByProviderId(row.Provider, settings);
                bool originalDeleted = false;
                bool thumbnailDeleted = false;

                // 1. Delete original
                try
                {
                    await provider.DeleteAsync(row.StorageKey, ct);
                    originalDeleted = true;
                }
                catch (FileNotFoundException)
                {
                    originalDeleted = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete original for expired pending upload {Id} via provider {Provider}", row.Id, row.Provider);
                }

                // 2. Delete thumbnail
                if (string.IsNullOrWhiteSpace(row.ThumbnailKey))
                {
                    thumbnailDeleted = true;
                }
                else
                {
                    try
                    {
                        await provider.DeleteAsync(row.ThumbnailKey, ct);
                        thumbnailDeleted = true;
                    }
                    catch (FileNotFoundException)
                    {
                        thumbnailDeleted = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete thumbnail for expired pending upload {Id} via provider {Provider}", row.Id, row.Provider);
                    }
                }

                // Chỉ mark PURGED nếu cả hai hoàn thành thành công
                if (originalDeleted && thumbnailDeleted)
                {
                    row.Status = "PURGED";
                    row.PurgedAt = DateTimeOffset.UtcNow;
                    cleanedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error purging storage objects for expired pending upload {Id}", row.Id);
            }
        }

        if (cleanedCount > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Cleaned up {Count} expired pending uploads", cleanedCount);
        }

        return cleanedCount;
    }
}
