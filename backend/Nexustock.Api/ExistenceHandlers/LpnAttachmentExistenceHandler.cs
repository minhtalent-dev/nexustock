using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Lpn.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class LpnAttachmentExistenceHandler : IEntityExistenceHandler
{
    private readonly LpnDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LpnAttachmentExistenceHandler(LpnDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("LPN", StringComparison.OrdinalIgnoreCase);

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

        return await _dbContext.Lpns
            .AsNoTracking()
            .AnyAsync(l => l.Id == entityId && l.TenantId == tenantId, ct);
    }
}
