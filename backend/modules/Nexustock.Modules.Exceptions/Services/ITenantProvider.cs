using System;

namespace Nexustock.Modules.Exceptions.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
