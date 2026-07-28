using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Entities;
using System.IO;

namespace Nexustock.Modules.Files.Services;

public interface IAttachmentObjectPurgeService
{
    Task<bool> PurgeAttachmentFilesAsync(Guid attachmentId, CancellationToken ct);
}

public sealed class AttachmentObjectPurgeService : IAttachmentObjectPurgeService
{
    private readonly FilesDbContext _db;
    private readonly FileStorageService _storage;
    private readonly IObjectStorageResolver _resolver;
    private readonly ILogger<AttachmentObjectPurgeService> _logger;

    public AttachmentObjectPurgeService(
        FilesDbContext db,
        FileStorageService storage,
        IObjectStorageResolver resolver,
        ILogger<AttachmentObjectPurgeService> logger)
    {
        _db = db;
        _storage = storage;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<bool> PurgeAttachmentFilesAsync(Guid attachmentId, CancellationToken ct)
    {
        // Sử dụng IgnoreQueryFilters để truy cập bản ghi đã soft-deleted (DeletedAt != null)
        var row = await _db.FileAttachments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

        if (row == null)
        {
            _logger.LogWarning("Attachment {Id} not found for purging", attachmentId);
            return false;
        }

        if (row.ObjectsPurgedAt != null)
        {
            // Đã được purge trước đó
            return true;
        }

        var settings = await _storage.GetOrCreateSettingsAsync(ct);
        var provider = _resolver.ResolveByProviderId(row.Provider, settings);

        bool originalDeleted = false;
        bool thumbnailDeleted = false;

        // 1. Delete original
        if (string.IsNullOrWhiteSpace(row.StorageKey))
        {
            originalDeleted = true;
        }
        else
        {
            try
            {
                await provider.DeleteAsync(row.StorageKey, ct);
                originalDeleted = true;
            }
            catch (FileNotFoundException)
            {
                // Nếu file gốc không tồn tại trên provider, coi như đã được delete thành công
                originalDeleted = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to purge original for attachment {Id} via provider {Provider}", attachmentId, row.Provider);
            }
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
                _logger.LogWarning(ex, "Failed to purge thumbnail for attachment {Id} via provider {Provider}", attachmentId, row.Provider);
            }
        }

        // Chỉ đánh dấu ObjectsPurgedAt khi cả hai đều thành công
        if (originalDeleted && thumbnailDeleted)
        {
            row.ObjectsPurgedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully purged all objects for attachment {Id}", attachmentId);
            return true;
        }

        return false;
    }
}
