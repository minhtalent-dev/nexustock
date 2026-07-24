using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Rma.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class RmaRequestExistenceHandler : IEntityExistenceHandler
{
    private readonly RmaDbContext _dbContext;

    public RmaRequestExistenceHandler(RmaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool CanHandle(string entityType) => entityType.Equals("RMA_REQUEST", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        return await _dbContext.RmaRequests.AnyAsync(r => r.Id == entityId, ct);
    }
}
