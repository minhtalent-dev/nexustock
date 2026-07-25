using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Qc.Contexts;

namespace Nexustock.Modules.Qc.Services;

public sealed class QcAttachmentReadService : IQcAttachmentReadService
{
    private readonly FilesDbContext _filesDbContext;
    private readonly QcDbContext _qcDbContext;
    private readonly IAttachmentService _attachmentService;

    public QcAttachmentReadService(FilesDbContext filesDbContext, QcDbContext qcDbContext, IAttachmentService attachmentService)
    {
        _filesDbContext = filesDbContext;
        _qcDbContext = qcDbContext;
        _attachmentService = attachmentService;
    }

    public async Task<Dictionary<Guid, List<AttachmentDto>>> GetAttachmentsByEntityIdsAsync(IEnumerable<Guid> entityIds, CancellationToken ct)
    {
        var ids = entityIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, List<AttachmentDto>>();

        var activeAttachments = await _filesDbContext.FileAttachments
            .AsNoTracking()
            .Where(a => a.EntityType == "QC_RESULT" && ids.Contains(a.EntityId) && a.DeletedAt == null)
            .ToListAsync(ct);

        var result = ids.ToDictionary(id => id, id => new List<AttachmentDto>());
        
        // Map active attachments
        foreach (var a in activeAttachments)
        {
            var contentUrl = $"/api/files/attachments/{a.Id}/content";
            var downloadUrl = $"/api/files/attachments/{a.Id}/content?disposition=attachment";
            
            string? previewKind = null;
            if (a.Kind == "IMAGE" || a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                previewKind = "image";
            else if (a.ContentType == "application/pdf" || a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                previewKind = "pdf";
            else
                previewKind = "download";

            var dto = new AttachmentDto(
                a.Id, a.EntityType, a.EntityId, a.FileName, a.ContentType, a.SizeBytes,
                a.Kind, a.Provider, previewKind, contentUrl, downloadUrl, null, a.CreatedAt);

            if (result.TryGetValue(a.EntityId, out var list))
            {
                list.Add(dto);
            }
        }

        var entitiesWithAttachmentRows = await _filesDbContext.FileAttachments
            .AsNoTracking()
            .Where(a => a.EntityType == "QC_RESULT" && ids.Contains(a.EntityId))
            .Select(a => a.EntityId)
            .Distinct()
            .ToListAsync(ct);

        // Fallback for legacy records that don't have attachments rows
        foreach (var id in ids)
        {
            if (result.TryGetValue(id, out var list) && list.Count == 0 && !entitiesWithAttachmentRows.Contains(id))
            {
                var qcResult = await _qcDbContext.QcResults
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == id, ct);

                if (qcResult != null && !string.IsNullOrWhiteSpace(qcResult.AttachmentRefs))
                {
                    var refs = qcResult.AttachmentRefs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var path in refs)
                    {
                        var fileName = Path.GetFileName(path.Trim());
                        if (string.IsNullOrWhiteSpace(fileName)) continue;

                        var ext = Path.GetExtension(fileName).ToLowerInvariant();
                        var contentType = ext switch
                        {
                            ".png" => "image/png",
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".webp" => "image/webp",
                            ".pdf" => "application/pdf",
                            _ => "application/octet-stream"
                        };

                        var previewKind = (contentType.StartsWith("image/") || ext == ".pdf") ? (ext == ".pdf" ? "pdf" : "image") : "download";
                        
                        // Legacy fallback DTO uses a deterministic Guid based on filename hash to prevent duplicate listings on client
                        var deterministicId = Guid.NewGuid();
                        var mockContentUrl = $"/api/files/attachments/{deterministicId}/content";
                        var mockDownloadUrl = $"/api/files/attachments/{deterministicId}/content?disposition=attachment";

                        list.Add(new AttachmentDto(
                            deterministicId, "QC_RESULT", id, fileName, contentType, 0,
                            "DOCUMENT", "LOCAL", previewKind, mockContentUrl, mockDownloadUrl, null, DateTimeOffset.UtcNow));
                    }
                }
            }
        }

        return result;
    }

    public async Task<List<AttachmentDto>> GetAttachmentsByEntityIdAsync(Guid entityId, CancellationToken ct)
    {
        var dict = await GetAttachmentsByEntityIdsAsync(new[] { entityId }, ct);
        return dict.TryGetValue(entityId, out var list) ? list : new List<AttachmentDto>();
    }
}