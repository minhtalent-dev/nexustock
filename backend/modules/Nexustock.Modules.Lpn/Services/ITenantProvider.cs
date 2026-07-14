using System;

namespace Nexustock.Modules.Lpn.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
