using System;

namespace Nexustock.Modules.Inventory.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
