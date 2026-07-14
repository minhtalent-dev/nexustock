using System;

namespace Nexustock.Modules.Replenishment.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
