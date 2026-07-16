using System;

namespace Nexustock.Modules.LocalAgent.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
