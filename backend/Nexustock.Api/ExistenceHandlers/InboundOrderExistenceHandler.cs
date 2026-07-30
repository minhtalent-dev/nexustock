using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Inbound.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class InboundOrderExistenceHandler : IEntityExistenceHandler
{
    private readonly InboundDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InboundOrderExistenceHandler(InboundDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("INBOUND_ORDER", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
        Guid tenantId;
        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var parsedTenantId))
        {
            tenantId = parsedTenantId;
        }
        else
        {
            tenantId = _dbContext.CurrentTenantId;
        }

        return await _dbContext.InboundOrders
            .AsNoTracking()
            .AnyAsync(o => o.Id == entityId && o.TenantId == tenantId, ct);
    }
}
