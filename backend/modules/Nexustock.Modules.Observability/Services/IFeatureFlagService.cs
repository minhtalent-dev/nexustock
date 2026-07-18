using System;
using System.Threading.Tasks;

namespace Nexustock.Modules.Observability.Services;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string flagName, Guid? userId = null);
}
