namespace Nexustock.LocalAgent.Devices.Scale;

public sealed class MockScaleDevice : IScaleDevice
{
    private readonly ScaleDeviceConfig _config;
    private readonly ScaleFrameParser _parser;
    private readonly StableWeightFilter _filter;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private decimal _offsetKg;

    public event EventHandler<ScaleReading>? ReadingChanged;

    public ScaleReading Current { get; private set; }

    public MockScaleDevice(ScaleDeviceConfig config, ScaleFrameParser parser)
    {
        _config = config;
        _parser = parser;
        _filter = new StableWeightFilter(config);
        Current = new ScaleReading(config.DeviceId, 0m, false, string.Empty, config.ScaleProfile, "disconnected", DateTimeOffset.UtcNow);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is { IsCompleted: false }) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts == null || _loop == null) return;

        await _cts.CancelAsync();
        try
        {
            await _loop.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public Task ZeroAsync(CancellationToken cancellationToken)
    {
        _offsetKg = Current.WeightKg;
        _filter.Reset();
        return Task.CompletedTask;
    }

    public Task TareAsync(CancellationToken cancellationToken)
    {
        _offsetKg = Current.WeightKg;
        _filter.Reset();
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var index = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var rawFrame = CreateFrame(index++);
            if (string.Equals(_config.MockMode, "error", StringComparison.OrdinalIgnoreCase))
            {
                Publish(new ScaleReading(_config.DeviceId, 0m, false, rawFrame, _config.ScaleProfile, "error", now, "scale.mock_error"));
            }
            else if (_parser.TryParse(rawFrame, _config.ScaleProfile, out var parsed, out var errorCode))
            {
                var weight = Math.Max(0m, parsed - _offsetKg);
                var stable = _filter.Add(weight, now);
                Publish(new ScaleReading(_config.DeviceId, weight, stable, rawFrame, _config.ScaleProfile, "connected", now));
            }
            else
            {
                Publish(new ScaleReading(_config.DeviceId, 0m, false, rawFrame, _config.ScaleProfile, "error", now, errorCode));
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private string CreateFrame(int index)
    {
        if (string.Equals(_config.MockMode, "unstable", StringComparison.OrdinalIgnoreCase))
        {
            var value = 12.00m + (index % 4) * 0.08m;
            return $"US,GS,+{value:0000.00}kg";
        }

        var stableValue = 12.35m + (index % 2) * 0.01m;
        return $"ST,GS,+{stableValue:0000.00}kg";
    }

    private void Publish(ScaleReading reading)
    {
        Current = reading;
        ReadingChanged?.Invoke(this, reading);
    }
}
