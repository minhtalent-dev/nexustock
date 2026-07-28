using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Entities;
using System.Collections.Concurrent;
using System.IO;

namespace Nexustock.Modules.Files.Services;

public interface IThumbnailBackfillService
{
    Task<int> BackfillThumbnailsAsync(CancellationToken ct);
}

public sealed class ThumbnailBackfillService : IThumbnailBackfillService
{
    private static readonly ConcurrentDictionary<Guid, int> FailAttempts = new();

    private readonly FilesDbContext _db;
    private readonly FileStorageService _storage;
    private readonly IObjectStorageResolver _resolver;
    private readonly IThumbnailService _thumbnailService;
    private readonly ThumbnailOptions _options;
    private readonly ILogger<ThumbnailBackfillService> _logger;

    public ThumbnailBackfillService(
        FilesDbContext db,
        FileStorageService storage,
        IObjectStorageResolver resolver,
        IThumbnailService thumbnailService,
        IOptions<ThumbnailOptions> options,
        ILogger<ThumbnailBackfillService> logger)
    {
        _db = db;
        _storage = storage;
        _resolver = resolver;
        _thumbnailService = thumbnailService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> BackfillThumbnailsAsync(CancellationToken ct)
    {
        if (!_options.Enabled || !_options.BackfillEnabled)
            return 0;

        // Lấy danh sách ảnh chưa có thumbnail, loại trừ các file đã quá 3 lần lỗi trong phiên chạy này
        var query = _db.FileAttachments
            .AsNoTracking()
            .Where(a => a.DeletedAt == null && a.ThumbnailKey == null)
            .Where(a => a.Kind == "IMAGE" || a.ContentType.StartsWith("image/"));

        var allPending = await query
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Take(_options.BatchSize * 2) // Lấy dư để loại trừ các ảnh lỗi cache
            .Select(a => new BackfillCandidate(
                a.Id,
                a.TenantId,
                a.Provider,
                a.StorageKey,
                a.ContentType))
            .ToListAsync(ct);

        var eligible = allPending
            .Where(a => !FailAttempts.TryGetValue(a.Id, out var count) || count < _options.MaxRetriesPerRun)
            .Take(_options.BatchSize)
            .ToList();

        if (eligible.Count == 0) return 0;

        int successCount = 0;
        var settings = await _storage.GetOrCreateSettingsAsync(ct);

        foreach (var att in eligible)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var provider = _resolver.ResolveByProviderId(att.Provider, settings);

                // 1. Đọc file original
                await using var origStream = await provider.OpenReadAsync(att.StorageKey, ct);
                
                // Đọc 12 bytes magic bytes để check
                byte[] header = new byte[12];
                int bytesRead = await origStream.ReadAsync(header.AsMemory(0, 12), ct);
                
                if (origStream.CanSeek)
                {
                    origStream.Position = 0;
                }

                if (!_thumbnailService.CanGenerate(att.ContentType, header))
                {
                    // Đánh dấu để không scan lại
                    FailAttempts.TryAdd(att.Id, _options.MaxRetriesPerRun);
                    _logger.LogInformation("Skip thumbnail backfill for attachment {Id}: format not supported by magic bytes", att.Id);
                    continue;
                }

                // 2. Generate thumbnail
                var thumbKey = _thumbnailService.BuildKey(att.StorageKey);
                await using var thumbStream = await _thumbnailService.GenerateAsync(origStream, ct);

                // 3. Put thumbnail lên storage
                await provider.PutAsync(thumbKey, thumbStream, "image/jpeg", ct);

                // 4. Chỉ gắn thumbnail nếu original vẫn đúng snapshot đã xử lý.
                var affected = await _db.FileAttachments
                    .Where(a => a.Id == att.Id
                        && a.TenantId == att.TenantId
                        && a.DeletedAt == null
                        && a.ThumbnailKey == null
                        && a.Provider == att.Provider
                        && a.StorageKey == att.StorageKey)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(a => a.ThumbnailKey, thumbKey),
                        ct);

                if (affected == 1)
                {
                    successCount++;
                    _logger.LogInformation("Successfully backfilled thumbnail for attachment {Id} via provider {Provider}", att.Id, att.Provider);
                    FailAttempts.TryRemove(att.Id, out _);
                    continue;
                }

                var winner = await _db.FileAttachments
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == att.Id && a.TenantId == att.TenantId, ct);

                var sameObjectWon = winner is not null
                    && winner.DeletedAt == null
                    && winner.Provider == att.Provider
                    && winner.StorageKey == att.StorageKey
                    && winner.ThumbnailKey == thumbKey;

                if (!sameObjectWon)
                {
                    try
                    {
                        await provider.DeleteAsync(thumbKey, ct);
                    }
                    catch (FileNotFoundException)
                    {
                        // Object đã được dọn bởi tiến trình cạnh tranh.
                    }
                    catch (Exception delEx)
                    {
                        _logger.LogWarning(delEx, "Failed to cleanup orphan thumbnail for attachment {Id} via provider {Provider}", att.Id, att.Provider);
                    }
                }

                _logger.LogInformation("Skipped stale thumbnail backfill for attachment {Id} via provider {Provider}", att.Id, att.Provider);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                int currentFailCount = FailAttempts.AddOrUpdate(att.Id, 1, (_, val) => val + 1);
                _logger.LogWarning(ex, "Failed to backfill thumbnail for attachment {Id} via provider {Provider}. Attempt {Attempt}", att.Id, att.Provider, currentFailCount);
            }
        }

        return successCount;
    }

    private sealed record BackfillCandidate(
        Guid Id,
        Guid TenantId,
        string Provider,
        string StorageKey,
        string ContentType);
}
