using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.Modules.Files.Services;

public interface IAttachmentService
{
    Task<AttachmentDto> BindAsync(BindAttachmentRequest request, string? user, CancellationToken ct);
    Task<IReadOnlyList<AttachmentDto>> ListAsync(string entityType, Guid entityId, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<AttachmentContent> OpenContentAsync(Guid id, string disposition, CancellationToken ct);
    Task<AttachmentContent> OpenThumbnailAsync(Guid id, CancellationToken ct);
}

public sealed class AttachmentService : IAttachmentService
{
    private static readonly HashSet<string> AllowedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRODUCT", "QC_RESULT", "INBOUND_ORDER", "SHIPMENT", "STOCKTAKE", "RMA_REQUEST",
        "LOT", "EXCEPTION", "LPN", "WAVE", "PUTAWAY_PROPOSAL", "CROSS_DOCK_CANDIDATE"
    };

    private readonly FilesDbContext _db;
    private readonly MasterDataDbContext _masterData;
    private readonly InventoryDbContext _inventory;
    private readonly IObjectStorageResolver _resolver;
    private readonly FileStorageService _storage;
    private readonly IAttachmentObjectPurgeService _purgeService;
    private readonly IEnumerable<IEntityExistenceHandler> _existenceHandlers;
    private readonly IEnumerable<IAttachmentLifecycleObserver> _observers;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        FilesDbContext db,
        MasterDataDbContext masterData,
        InventoryDbContext inventory,
        IObjectStorageResolver resolver,
        FileStorageService storage,
        IAttachmentObjectPurgeService purgeService,
        IEnumerable<IEntityExistenceHandler> existenceHandlers,
        IEnumerable<IAttachmentLifecycleObserver> observers,
        ILogger<AttachmentService> logger)
    {
        _db = db;
        _masterData = masterData;
        _inventory = inventory;
        _resolver = resolver;
        _storage = storage;
        _purgeService = purgeService;
        _existenceHandlers = existenceHandlers;
        _observers = observers;
        _logger = logger;
    }

    public async Task<AttachmentDto> BindAsync(BindAttachmentRequest request, string? user, CancellationToken ct)
    {
        var entityType = request.EntityType.Trim().ToUpperInvariant();
        if (!AllowedEntityTypes.Contains(entityType))
            throw new FileDomainException("ENTITY_TYPE_NOT_ALLOWED", "Entity type is not allowed");

        FilePendingUpload? pending = null;
        if (request.UploadId.HasValue)
        {
            // Check if already bound idempotently
            var existing = await _db.FileAttachments
                .FirstOrDefaultAsync(a => a.PendingUploadId == request.UploadId.Value && a.DeletedAt == null, ct);
            if (existing != null)
                return ToDto(existing);

            pending = await _db.FilePendingUploads
                .FirstOrDefaultAsync(p => p.Id == request.UploadId.Value, ct);
            if (pending == null)
                throw new FileDomainException("UPLOAD_NOT_FOUND", "Pending upload not found", 404);
            if (pending.Status == "PURGED")
                throw new FileDomainException("UPLOAD_ALREADY_PURGED", "Upload object has been purged", 409);
            if (pending.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new FileDomainException("UPLOAD_EXPIRED", "Pending upload has expired", 409);
        }

        bool exists = false;
        if (entityType == "PRODUCT")
        {
            exists = await _masterData.Products.AnyAsync(p => p.Id == request.EntityId, ct);
        }
        else if (entityType == "SHIPMENT")
        {
            exists = await _inventory.Shipments.AnyAsync(s => s.Id == request.EntityId, ct);
        }
        else if (entityType == "STOCKTAKE")
        {
            exists = await _inventory.Stocktakes.AnyAsync(s => s.Id == request.EntityId, ct);
        }
        else
        {
            var handler = _existenceHandlers.FirstOrDefault(h => h.CanHandle(entityType));
            if (handler != null)
            {
                exists = await handler.ExistsAsync(request.EntityId, ct);
            }
        }

        if (!exists)
            throw new FileDomainException("ATTACHMENT_ENTITY_NOT_FOUND", $"{entityType} not found", 404);

        var attachmentId = Guid.NewGuid();
        var row = new FileAttachment
        {
            Id = attachmentId,
            TenantId = _db.CurrentTenantId,
            EntityType = entityType,
            EntityId = request.EntityId,
            FileName = pending?.FileName ?? "unnamed",
            ContentType = pending?.ContentType ?? "application/octet-stream",
            SizeBytes = pending?.SizeBytes ?? 0,
            Kind = pending?.Kind ?? "DOCUMENT",
            Provider = pending?.Provider ?? "LOCAL",
            StorageKey = pending?.StorageKey ?? "",
            PublicUrl = pending?.LegacyUrl ?? "",
            PendingUploadId = request.UploadId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = user,
            ThumbnailKey = pending?.ThumbnailKey
        };

        if (pending != null)
        {
            pending.Status = "BOUND";
            pending.BoundAt = DateTimeOffset.UtcNow;
            pending.AttachmentId = attachmentId;
        }

        _db.FileAttachments.Add(row);
        
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Tránh race condition khi 2 bind đồng thời gửi cùng uploadId
            _logger.LogWarning(ex, "DbUpdateException on binding upload {UploadId}. Checking for duplicate bind.", request.UploadId);
            if (request.UploadId.HasValue)
            {
                var concurrentBound = await _db.FileAttachments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.PendingUploadId == request.UploadId.Value && a.DeletedAt == null, ct);
                if (concurrentBound != null)
                {
                    return ToDto(concurrentBound);
                }
            }
            throw;
        }

        // Gọi observers sau khi commit thành công
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnBoundAsync(_db.CurrentTenantId, entityType, request.EntityId, attachmentId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attachment observer {Observer} failed during OnBound", observer.GetType().Name);
            }
        }

        return ToDto(row);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        var type = entityType.Trim().ToUpperInvariant();
        var items = await _db.FileAttachments
            .Where(a => a.EntityType == type && a.EntityId == entityId && a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.FileAttachments.FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null, ct);
        if (row == null)
            throw new FileDomainException("ATTACHMENT_NOT_FOUND", "Attachment not found", 404);

        row.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Gọi observers thông báo delete
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnDeletedAsync(_db.CurrentTenantId, row.EntityType, row.EntityId, row.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attachment observer {Observer} failed during OnDeleted", observer.GetType().Name);
            }
        }

        try
        {
            await _purgeService.PurgeAttachmentFilesAsync(row.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Synchronous files purge failed for attachment {Id}, deferred to background worker", row.Id);
        }
    }

    public async Task<AttachmentContent> OpenContentAsync(Guid id, string disposition, CancellationToken ct)
    {
        var row = await _db.FileAttachments.FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null, ct);
        if (row == null)
            throw new FileDomainException("ATTACHMENT_NOT_FOUND", "Attachment not found", 404);

        var settings = await _storage.GetOrCreateSettingsAsync(ct);
        var provider = _resolver.ResolveByProviderId(row.Provider, settings);

        try
        {
            var stream = await provider.OpenReadAsync(row.StorageKey, ct);
            
            // Structured event logs cho view/download
            if (disposition.Equals("inline", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("files.attachment.view id={Id} entityType={EntityType} provider={Provider} disposition={Disposition}", 
                    row.Id, row.EntityType, row.Provider, disposition);
            }
            else
            {
                _logger.LogInformation("files.attachment.download id={Id} entityType={EntityType} provider={Provider} disposition={Disposition}", 
                    row.Id, row.EntityType, row.Provider, disposition);
            }

            return new AttachmentContent(stream, row.ContentType, row.FileName);
        }
        catch (FileNotFoundException)
        {
            throw new FileDomainException("ATTACHMENT_CONTENT_NOT_FOUND", "Attachment file missing from storage provider", 404);
        }
        catch (Exception ex) when (ex is not FileDomainException)
        {
            _logger.LogError(ex, "Failed to open read stream for attachment {Id}", id);
            throw new FileDomainException("STORAGE_PROVIDER_ERROR", "Storage provider read error", 503);
        }
    }

    public async Task<AttachmentContent> OpenThumbnailAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.FileAttachments.FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null, ct);
        if (row == null)
            throw new FileDomainException("ATTACHMENT_NOT_FOUND", "Attachment not found", 404);

        if (string.IsNullOrWhiteSpace(row.ThumbnailKey))
            throw new FileDomainException("THUMBNAIL_NOT_FOUND", "Thumbnail not found for this attachment", 404);

        var settings = await _storage.GetOrCreateSettingsAsync(ct);
        var provider = _resolver.ResolveByProviderId(row.Provider, settings);

        try
        {
            var stream = await provider.OpenReadAsync(row.ThumbnailKey, ct);
            _logger.LogInformation("files.attachment.view_thumbnail id={Id} provider={Provider}", row.Id, row.Provider);

            var rawInput = $"{row.Id:N}:{row.ThumbnailKey}";
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(rawInput);
            var hashBytes = System.Security.Cryptography.SHA256.HashData(inputBytes);
            var etag = $"\"{Convert.ToHexString(hashBytes).ToLowerInvariant()}\"";

            var thumbName = Path.GetFileNameWithoutExtension(row.FileName) + ".thumb.jpg";
            return new AttachmentContent(stream, "image/jpeg", thumbName, etag);
        }
        catch (FileNotFoundException)
        {
            throw new FileDomainException("ATTACHMENT_CONTENT_NOT_FOUND", "Thumbnail file missing from storage provider", 404);
        }
        catch (Exception ex) when (ex is not FileDomainException)
        {
            _logger.LogError(ex, "Failed to open read stream for thumbnail {Id}", id);
            throw new FileDomainException("STORAGE_PROVIDER_ERROR", "Storage provider read error", 503);
        }
    }


    private static AttachmentDto ToDto(FileAttachment a)
    {
        var contentUrl = $"/api/files/attachments/{a.Id}/content";
        var downloadUrl = $"/api/files/attachments/{a.Id}/content?disposition=attachment";
        
        string? previewKind = null;
        if (a.Kind == "IMAGE" || a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            previewKind = "image";
        }
        else if (a.ContentType == "application/pdf" || a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            previewKind = "pdf";
        }
        else
        {
            previewKind = "download";
        }

        var thumbnailUrl = !string.IsNullOrWhiteSpace(a.ThumbnailKey) ? $"/api/files/attachments/{a.Id}/thumbnail" : null;

        return new AttachmentDto(
            a.Id, a.EntityType, a.EntityId, a.FileName, a.ContentType, a.SizeBytes,
            a.Kind, a.Provider, previewKind, contentUrl, downloadUrl, thumbnailUrl, a.CreatedAt);
    }
}
