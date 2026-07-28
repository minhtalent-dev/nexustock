using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Rma.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class RmaRequestExistenceHandler : IEntityExistenceHandler
{
    private readonly RmaDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RmaRequestExistenceHandler(RmaDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("RMA_REQUEST", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        return await _dbContext.RmaRequests
            .AsNoTracking()
            .AnyAsync(r => r.Id == entityId && r.TenantId == tenantId, ct);
    }
}
