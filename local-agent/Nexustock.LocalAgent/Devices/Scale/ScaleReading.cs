namespace Nexustock.LocalAgent.Devices.Scale;

public sealed record ScaleReading(
    string DeviceId,
    decimal WeightKg,
    bool Stable,
    string RawFrame,
    string Profile,
    string ConnectionState,
    DateTimeOffset Timestamp,
    string? ErrorCode = null);
