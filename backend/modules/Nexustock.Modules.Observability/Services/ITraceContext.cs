namespace Nexustock.Modules.Observability.Services;

public interface ITraceContext
{
    string GetCurrentTraceId();
}
