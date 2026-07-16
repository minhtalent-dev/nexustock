namespace Nexustock.LocalAgent.Devices.Scale;

public interface IScaleDevice
{
    event EventHandler<ScaleReading>? ReadingChanged;
    ScaleReading Current { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task ZeroAsync(CancellationToken cancellationToken);
    Task TareAsync(CancellationToken cancellationToken);
}
