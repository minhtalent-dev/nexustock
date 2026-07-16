namespace Nexustock.LocalAgent.Devices.Scale;

public class ScaleDeviceConfig
{
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "mock";
    public string DeviceId { get; set; } = "scale_01";
    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 9600;
    public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "One";
    public string LineEnding { get; set; } = "\r\n";
    public string ScaleProfile { get; set; } = "generic-rs232";
    public int StableWindowMs { get; set; } = 800;
    public decimal StableToleranceKg { get; set; } = 0.02m;
    public decimal MinimumWeightKg { get; set; } = 0.001m;
    public int ReadTimeoutMs { get; set; } = 500;
    public string MockMode { get; set; } = "stable";
}
