namespace Nexustock.LocalAgent.Devices.Printer;

public interface IPrinterDevice
{
    string PrinterCode { get; }
    string Language { get; }
    Task<PrinterResult> PrintAsync(string rawCommand, CancellationToken cancellationToken);
    Task<string> GetStatusAsync(CancellationToken cancellationToken);
}

public record PrinterResult(bool Success, string Status, string? ErrorCode = null, string? ErrorMessage = null);
