using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Wave.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class WaveAttachmentExistenceHandler : IEntityExistenceHandler
{
    private readonly WaveDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WaveAttachmentExistenceHandler(WaveDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("WAVE", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        return await _dbContext.PickingWaves
            .AsNoTracking()
            .AnyAsync(w => w.Id == entityId && w.TenantId == tenantId, ct);
    }
}
