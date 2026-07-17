using System.IO;

namespace Nexustock.LocalAgent.Devices.Printer;

public class MockPrinterDevice : IPrinterDevice
{
    private readonly PrinterDeviceConfig _config;

    public MockPrinterDevice(PrinterDeviceConfig config)
    {
        _config = config;
    }

    public string PrinterCode => _config.PrinterCode;
    public string Language => _config.Language;

    public async Task<PrinterResult> PrintAsync(string rawCommand, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(_config.MockOutputPath))
            {
                Directory.CreateDirectory(_config.MockOutputPath);
            }

            var fileName = $"print_{_config.PrinterCode}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.{_config.Language}";
            var filePath = Path.Combine(_config.MockOutputPath, fileName);

            await File.WriteAllTextAsync(filePath, rawCommand, cancellationToken);

            return new PrinterResult(true, "printed");
        }
        catch (Exception ex)
        {
            return new PrinterResult(false, "failed", "mock.write_error", ex.Message);
        }
    }

    public Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult("online");
    }
}
