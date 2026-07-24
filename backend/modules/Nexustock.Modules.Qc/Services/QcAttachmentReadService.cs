using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Services;

namespace Nexustock.Modules.Qc.Services;

public sealed class QcAttachmentReadService : IQcAttachmentReadService
{
    private readonly FilesDbContext _filesDbContext;
    private readonly IAttachmentService _attachmentService;

    public QcAttachmentReadService(FilesDbContext filesDbContext, IAttachmentService attachmentService)
    {
        _filesDbContext = filesDbContext;
        _attachmentService = attachmentService;
    }

    public async Task<Dictionary<Guid, List<AttachmentDto>>> GetAttachmentsByEntityIdsAsync(IEnumerable<Guid> entityIds, CancellationToken ct)
    {
        var ids = entityIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, List<AttachmentDto>>();

        var attachments = await _filesDbContext.FileAttachments
            .AsNoTracking()
            .Where(a => a.EntityType == "QC_RESULT" && ids.Contains(a.EntityId) && a.DeletedAt == null)
            .ToListAsync(ct);

        var result = ids.ToDictionary(id => id, id => new List<AttachmentDto>());
        
        foreach (var a in attachments)
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

        return result;
    }

    public async Task<List<AttachmentDto>> GetAttachmentsByEntityIdAsync(Guid entityId, CancellationToken ct)
    {
        var list = await _attachmentService.ListAsync("QC_RESULT", entityId, ct);
        return list.ToList();
    }
}