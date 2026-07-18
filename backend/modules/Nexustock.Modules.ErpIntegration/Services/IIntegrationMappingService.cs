using System;
using System.Threading.Tasks;

namespace Nexustock.Modules.ErpIntegration.Services;

public class UnresolvedMappingException : Exception
{
    public string ErrorCode { get; }
    public string ExternalCode { get; }

    public UnresolvedMappingException(string errorCode, string externalCode, string message) : base(message)
    {
        ErrorCode = errorCode;
        ExternalCode = externalCode;
    }
}

public interface IIntegrationMappingService
{
    Task<Guid> ResolveItemAsync(Guid tenantId, string externalSystem, string externalCode);
    Task<Guid> ResolveWarehouseAsync(Guid tenantId, string externalSystem, string externalCode);
    Task<Guid> ResolvePartnerAsync(Guid tenantId, string externalSystem, string externalCode);
    Task<Guid> ResolveUomAsync(Guid tenantId, string externalSystem, string externalCode);
}
