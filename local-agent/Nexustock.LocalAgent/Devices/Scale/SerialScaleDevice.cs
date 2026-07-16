namespace Nexustock.LocalAgent.Devices.Scale;

public sealed class SerialScaleDevice : IScaleDevice
{
    public event EventHandler<ScaleReading>? ReadingChanged;

    public ScaleReading Current { get; }

    public SerialScaleDevice(ScaleDeviceConfig config)
    {
        Current = new ScaleReading(config.DeviceId, 0m, false, string.Empty, config.ScaleProfile, "error", DateTimeOffset.UtcNow, "scale.serial_not_configured");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ReadingChanged?.Invoke(this, Current);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ZeroAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task TareAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
