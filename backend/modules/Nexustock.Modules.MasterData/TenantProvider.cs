using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData;

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(tenantClaim, out var tenantId))
                return tenantId;

            // Fallback: mặc định cho background job / không auth
            return Guid.Parse("00000000-0000-0000-0000-000000000001");
        }
    }
}
