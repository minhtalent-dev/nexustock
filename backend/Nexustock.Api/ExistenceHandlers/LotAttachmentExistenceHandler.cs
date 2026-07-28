using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Inbound.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class LotAttachmentExistenceHandler : IEntityExistenceHandler
{
    private readonly InboundDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LotAttachmentExistenceHandler(InboundDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("LOT", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        return await _dbContext.Lots
            .AsNoTracking()
            .AnyAsync(l => l.Id == entityId && l.TenantId == tenantId, ct);
    }
}
