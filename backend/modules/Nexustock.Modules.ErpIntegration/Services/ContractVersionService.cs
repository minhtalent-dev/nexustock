using System;
using Microsoft.Extensions.Logging;

namespace Nexustock.Modules.ErpIntegration.Services;

public class ContractVersionService : IContractVersionService
{
    private readonly ILogger<ContractVersionService> _logger;

    public ContractVersionService(ILogger<ContractVersionService> logger)
    {
        _logger = logger;
    }

    public ContractVersionStatus CheckVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return ContractVersionStatus.Retired;

        var cleanVersion = version.Trim().ToLowerInvariant();

        if (cleanVersion == "v1.1")
        {
            return ContractVersionStatus.Supported;
        }
        else if (cleanVersion == "v1.0")
        {
            _logger.LogWarning("Deprecated integration contract version {Version} is used.", version);
            return ContractVersionStatus.Deprecated;
        }

        return ContractVersionStatus.Retired;
    }
}
