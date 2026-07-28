using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Qc.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class QcResultExistenceHandler : IEntityExistenceHandler
{
    private readonly QcDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public QcResultExistenceHandler(QcDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("QC_RESULT", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        return await _dbContext.QcResults
            .AsNoTracking()
            .AnyAsync(q => q.Id == entityId && q.TenantId == tenantId, ct);
    }
}
