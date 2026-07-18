namespace Nexustock.Modules.ErpIntegration.Services;

public interface IPayloadHashService
{
    string ComputeHash(string jsonPayload);
}
