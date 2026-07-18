namespace Nexustock.Modules.ErpIntegration.Services;

public enum ContractVersionStatus
{
    Supported,
    Deprecated,
    Retired
}

public interface IContractVersionService
{
    ContractVersionStatus CheckVersion(string version);
}
