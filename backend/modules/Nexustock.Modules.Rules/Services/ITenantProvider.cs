using System;

namespace Nexustock.Modules.Rules.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
