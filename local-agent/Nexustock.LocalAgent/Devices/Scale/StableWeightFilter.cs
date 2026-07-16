namespace Nexustock.LocalAgent.Devices.Scale;

public sealed class StableWeightFilter
{
    private readonly Queue<(DateTimeOffset Timestamp, decimal WeightKg)> _samples = new();
    private readonly int _windowMs;
    private readonly decimal _toleranceKg;
    private readonly decimal _minimumWeightKg;

    public StableWeightFilter(ScaleDeviceConfig config)
    {
        _windowMs = Math.Max(100, config.StableWindowMs);
        _toleranceKg = Math.Max(0m, config.StableToleranceKg);
        _minimumWeightKg = Math.Max(0m, config.MinimumWeightKg);
    }

    public bool Add(decimal weightKg, DateTimeOffset timestamp)
    {
        _samples.Enqueue((timestamp, weightKg));

        var cutoff = timestamp.AddMilliseconds(-_windowMs);
        while (_samples.Count > 0 && _samples.Peek().Timestamp < cutoff)
        {
            _samples.Dequeue();
        }

        if (weightKg <= _minimumWeightKg || _samples.Count < 2)
        {
            return false;
        }

        var min = _samples.Min(s => s.WeightKg);
        var max = _samples.Max(s => s.WeightKg);
        return max - min <= _toleranceKg;
    }

    public void Reset() => _samples.Clear();
}
