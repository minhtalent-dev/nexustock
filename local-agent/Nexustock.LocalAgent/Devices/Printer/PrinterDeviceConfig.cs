namespace Nexustock.LocalAgent.Devices.Printer;

public class PrinterDeviceConfig
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "mock"; // mock, tcp, windows
    public string PrinterCode { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty; // For windows mode
    public string Host { get; set; } = string.Empty; // For tcp mode
    public int Port { get; set; } = 9100; // For tcp mode
    public string Language { get; set; } = "zpl"; // zpl, tspl
    public int WriteTimeoutMs { get; set; } = 5000;
    public string MockOutputPath { get; set; } = "mock_labels";
}
