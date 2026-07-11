using System;

namespace Nexustock.Modules.Qc.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
