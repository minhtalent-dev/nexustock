using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Inbound.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class InboundOrderExistenceHandler : IEntityExistenceHandler
{
    private readonly InboundDbContext _dbContext;

    public InboundOrderExistenceHandler(InboundDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool CanHandle(string entityType) => entityType.Equals("INBOUND_ORDER", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        return await _dbContext.InboundOrders.AnyAsync(o => o.Id == entityId, ct);
    }
}
