using System;

namespace Nexustock.Modules.Putaway.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
