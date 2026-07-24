namespace Nexustock.Modules.Files.Services;

public interface IEntityExistenceHandler
{
    bool CanHandle(string entityType);
    Task<bool> ExistsAsync(Guid entityId, CancellationToken ct);
}
