using System;
using Microsoft.AspNetCore.Http;

namespace Nexustock.Modules.Rules.Services;

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
            if (tenantClaim != null && Guid.TryParse(tenantClaim, out var tenantId))
            {
                return tenantId;
            }
            return Guid.Parse("00000000-0000-0000-0000-000000000001");
        }
    }
}
