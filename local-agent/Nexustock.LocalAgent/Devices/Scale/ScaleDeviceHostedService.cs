using Microsoft.Extensions.Hosting;

namespace Nexustock.LocalAgent.Devices.Scale;

public sealed class ScaleDeviceHostedService : IHostedService
{
    private readonly IScaleDevice _scaleDevice;
    private readonly ScaleDeviceConfig _config;

    public ScaleDeviceHostedService(IScaleDevice scaleDevice, ScaleDeviceConfig config)
    {
        _scaleDevice = scaleDevice;
        _config = config;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _config.Enabled ? _scaleDevice.StartAsync(cancellationToken) : Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _scaleDevice.StopAsync(cancellationToken);
    }
}
