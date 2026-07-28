using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Exceptions.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class ExceptionAttachmentExistenceHandler : IEntityExistenceHandler
{
    private readonly ExceptionsDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExceptionAttachmentExistenceHandler(ExceptionsDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string entityType) => entityType.Equals("EXCEPTION", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        return await _dbContext.OperationalExceptions
            .AsNoTracking()
            .AnyAsync(e => e.Id == entityId && e.TenantId == tenantId, ct);
    }
}
