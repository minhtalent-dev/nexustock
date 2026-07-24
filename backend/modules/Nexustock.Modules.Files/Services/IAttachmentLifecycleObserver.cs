namespace Nexustock.Modules.Files.Services;

public interface IAttachmentLifecycleObserver
{
    Task OnBoundAsync(Guid tenantId, string entityType, Guid entityId, Guid attachmentId, CancellationToken ct);
    Task OnDeletedAsync(Guid tenantId, string entityType, Guid entityId, Guid attachmentId, CancellationToken ct);
}