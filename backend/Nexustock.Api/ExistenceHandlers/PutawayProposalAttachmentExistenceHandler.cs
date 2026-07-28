using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Putaway.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class PutawayProposalAttachmentExistenceHandler : IEntityExistenceHandler
{
    private readonly PutawayDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PutawayProposalAttachmentExistenceHandler(PutawayDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("PUTAWAY_PROPOSAL", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        return await _dbContext.PutawayProposals
            .AsNoTracking()
            .AnyAsync(p => p.Id == entityId && p.TenantId == tenantId, ct);
    }
}
