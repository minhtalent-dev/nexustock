using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData;

public class TenantProvider : ITenantProvider
{
    public Guid TenantId => Guid.Parse("00000000-0000-0000-0000-000000000001");
}
