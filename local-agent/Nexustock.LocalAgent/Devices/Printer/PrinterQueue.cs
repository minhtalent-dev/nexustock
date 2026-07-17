using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Nexustock.LocalAgent.Devices.Printer;

public interface IPrinterQueue
{
    void Enqueue(string printerCode, Func<CancellationToken, Task> printAction);
}

public class PrinterQueue : IPrinterQueue, IDisposable
{
    private readonly ConcurrentDictionary<string, BlockingCollection<Func<CancellationToken, Task>>> _queues = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cts = new();
    private readonly ILogger<PrinterQueue> _logger;

    public PrinterQueue(ILogger<PrinterQueue> logger)
    {
        _logger = logger;
    }

    public void Enqueue(string printerCode, Func<CancellationToken, Task> printAction)
    {
        var queue = _queues.GetOrAdd(printerCode, code =>
        {
            var q = new BlockingCollection<Func<CancellationToken, Task>>();
            var cts = new CancellationTokenSource();
            _cts.TryAdd(code, cts);
            
            Task.Run(() => ProcessQueueAsync(code, q, cts.Token));
            return q;
        });

        queue.Add(printAction);
    }

    private async Task ProcessQueueAsync(string printerCode, BlockingCollection<Func<CancellationToken, Task>> queue, CancellationToken ct)
    {
        _logger.LogInformation("Bắt đầu hàng đợi in cho máy in: {PrinterCode}", printerCode);
        
        foreach (var action in queue.GetConsumingEnumerable(ct))
        {
            try
            {
                await action(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện lệnh in trong hàng đợi {PrinterCode}", printerCode);
            }
        }

        _logger.LogInformation("Dừng hàng đợi in cho máy in: {PrinterCode}", printerCode);
    }

    public void Dispose()
    {
        foreach (var cts in _cts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _queues.Clear();
    }
}
