namespace Nexustock.Modules.MasterData.Services;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
