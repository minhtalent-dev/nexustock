using System;

namespace Nexustock.Modules.Inbound.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
