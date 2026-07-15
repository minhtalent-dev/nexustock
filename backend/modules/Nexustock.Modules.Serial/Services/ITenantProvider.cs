using System;

namespace Nexustock.Modules.Serial.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
