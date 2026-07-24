using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Qc.Contexts;

namespace Nexustock.Modules.Qc.Services;

public sealed class QcAttachmentCompatibilityObserver : IAttachmentLifecycleObserver
{
    private readonly QcDbContext _qcContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QcAttachmentCompatibilityObserver> _logger;

    public QcAttachmentCompatibilityObserver(
        QcDbContext qcContext,
        IServiceProvider serviceProvider,
        ILogger<QcAttachmentCompatibilityObserver> logger)
    {
        _qcContext = qcContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task OnBoundAsync(Guid tenantId, string entityType, Guid entityId, Guid attachmentId, CancellationToken ct)
    {
        if (!string.Equals(entityType, "QC_RESULT", StringComparison.OrdinalIgnoreCase)) return;
        await SynchronizeSnapshotAsync(tenantId, entityId, ct);
    }

    public async Task OnDeletedAsync(Guid tenantId, string entityType, Guid entityId, Guid attachmentId, CancellationToken ct)
    {
        if (!string.Equals(entityType, "QC_RESULT", StringComparison.OrdinalIgnoreCase)) return;
        await SynchronizeSnapshotAsync(tenantId, entityId, ct);
    }

    private async Task SynchronizeSnapshotAsync(Guid tenantId, Guid entityId, CancellationToken ct)
    {
        try
        {
            var result = await _qcContext.QcResults
                .FirstOrDefaultAsync(r => r.Id == entityId && r.TenantId == tenantId, ct);
            if (result == null) return;

            var attachmentService = _serviceProvider.GetRequiredService<IAttachmentService>();
            var attachments = await attachmentService.ListAsync("QC_RESULT", entityId, ct);
            var refs = string.Join(",", attachments.Select(a => a.ContentUrl));
            
            result.AttachmentRefs = string.IsNullOrWhiteSpace(refs) ? null : refs;
            await _qcContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to synchronize compatibility snapshot for QC Result {ResultId}", entityId);
        }
    }
}