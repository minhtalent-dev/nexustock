using System;

namespace Nexustock.Modules.ErpIntegration.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
