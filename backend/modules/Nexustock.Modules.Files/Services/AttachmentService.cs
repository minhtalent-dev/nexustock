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
}

public sealed class AttachmentService : IAttachmentService
{
    private static readonly HashSet<string> AllowedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRODUCT", "QC_RESULT", "INBOUND_ORDER", "SHIPMENT", "STOCKTAKE", "RMA_REQUEST"
    };

    private readonly FilesDbContext _db;
    private readonly MasterDataDbContext _masterData;
    private readonly InventoryDbContext _inventory;
    private readonly IObjectStorageResolver _resolver;
    private readonly FileStorageService _storage;
    private readonly IEnumerable<IEntityExistenceHandler> _existenceHandlers;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        FilesDbContext db,
        MasterDataDbContext masterData,
        InventoryDbContext inventory,
        IObjectStorageResolver resolver,
        FileStorageService storage,
        IEnumerable<IEntityExistenceHandler> existenceHandlers,
        ILogger<AttachmentService> logger)
    {
        _db = db;
        _masterData = masterData;
        _inventory = inventory;
        _resolver = resolver;
        _storage = storage;
        _existenceHandlers = existenceHandlers;
        _logger = logger;
    }

    public async Task<AttachmentDto> BindAsync(BindAttachmentRequest request, string? user, CancellationToken ct)
    {
        var entityType = request.EntityType.Trim().ToUpperInvariant();
        if (!AllowedEntityTypes.Contains(entityType))
            throw new FileDomainException("ENTITY_TYPE_NOT_ALLOWED", "Entity type is not allowed");

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

        var row = new FileAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = _db.CurrentTenantId,
            EntityType = entityType,
            EntityId = request.EntityId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            Kind = request.Kind,
            Provider = request.Provider,
            StorageKey = request.StorageKey,
            PublicUrl = request.Url,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = user
        };
        _db.FileAttachments.Add(row);
        await _db.SaveChangesAsync(ct);
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

        try
        {
            var settings = await _storage.GetOrCreateSettingsAsync(ct);
            var provider = _resolver.ResolveByProviderId(row.Provider, settings);
            await provider.DeleteAsync(row.StorageKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "files.delete.object_failed id={Id} provider={Provider}", row.Id, row.Provider);
        }
    }

    private static AttachmentDto ToDto(FileAttachment a) => new(
        a.Id, a.EntityType, a.EntityId, a.FileName, a.ContentType, a.SizeBytes,
        a.Kind, a.Provider, a.StorageKey, a.PublicUrl, a.CreatedAt);
}
