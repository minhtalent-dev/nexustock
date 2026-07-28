using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.CrossDocking.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class CrossDockCandidateAttachmentExistenceHandler : IEntityExistenceHandler
{
    private readonly CrossDockingDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CrossDockCandidateAttachmentExistenceHandler(CrossDockingDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("CROSS_DOCK_CANDIDATE", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        return await _dbContext.Candidates
            .AsNoTracking()
            .AnyAsync(c => c.Id == entityId && c.TenantId == tenantId, ct);
    }
}
