using System.Net.Sockets;
using System.Text;

namespace Nexustock.LocalAgent.Devices.Printer;

public class TcpRawPrinterDevice : IPrinterDevice
{
    private const int MaxRetries = 2;
    private const int RetryDelayMs = 500;

    private readonly PrinterDeviceConfig _config;

    public TcpRawPrinterDevice(PrinterDeviceConfig config)
    {
        _config = config;
    }

    public string PrinterCode => _config.PrinterCode;
    public string Language => _config.Language;

    public async Task<PrinterResult> PrintAsync(string rawCommand, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_config.Host))
        {
            return new PrinterResult(false, "failed", "printer.command_rejected", "Thiếu cấu hình Host cho TCP printer.");
        }

        var rawBytes = Encoding.UTF8.GetBytes(rawCommand);

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new PrinterResult(false, "failed", "printer.timeout", "Lệnh in bị hủy do CancellationToken.");
            }

            try
            {
                using var client = new TcpClient();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_config.WriteTimeoutMs);

                try
                {
                    await client.ConnectAsync(_config.Host, _config.Port, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout (không phải caller cancel)
                    return new PrinterResult(false, "failed", "printer.timeout", $"Kết nối TCP tới {_config.Host}:{_config.Port} vượt quá timeout {_config.WriteTimeoutMs}ms.");
                }

                using var stream = client.GetStream();
                stream.WriteTimeout = _config.WriteTimeoutMs;

                await stream.WriteAsync(rawBytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                return new PrinterResult(true, "printed");
            }
            catch (OperationCanceledException)
            {
                return new PrinterResult(false, "failed", "printer.timeout", "Lệnh in bị hủy.");
            }
            catch (SocketException ex) when (attempt < MaxRetries)
            {
                // Transient network error — retry
                await Task.Delay(RetryDelayMs, cancellationToken);
                _ = ex;
            }
            catch (Exception ex)
            {
                return new PrinterResult(false, "failed", "printer.timeout", $"Lỗi TCP khi in: {ex.Message}");
            }
        }

        return new PrinterResult(false, "failed", "printer.offline", $"Máy in TCP {_config.Host}:{_config.Port} không phản hồi sau {MaxRetries} lần thử.");
    }

    public Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            var connected = client.ConnectAsync(_config.Host, _config.Port).Wait(_config.WriteTimeoutMs);
            return Task.FromResult(connected ? "online" : "offline");
        }
        catch
        {
            return Task.FromResult("offline");
        }
    }
}
