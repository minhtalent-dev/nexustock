using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexustock.Modules.Files.Services;

namespace Nexustock.Modules.Files.Workers;

public sealed class ThumbnailBackfillWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ThumbnailOptions _options;
    private readonly ILogger<ThumbnailBackfillWorker> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    public ThumbnailBackfillWorker(
        IServiceProvider serviceProvider,
        IOptions<ThumbnailOptions> options,
        ILogger<ThumbnailBackfillWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.BackfillEnabled)
        {
            _logger.LogInformation("Thumbnail backfill worker is disabled via options configuration");
            return;
        }

        // Startup delay để tránh tranh chấp tài nguyên lúc khởi động
        var delaySeconds = Math.Max(10, _options.StartupDelaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

        _logger.LogInformation("ThumbnailBackfillWorker started. CheckInterval={Interval}", CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var backfillService = scope.ServiceProvider.GetRequiredService<IThumbnailBackfillService>();
                
                int processedCount = await backfillService.BackfillThumbnailsAsync(stoppingToken);
                if (processedCount > 0)
                {
                    _logger.LogInformation("Successfully backfilled {Count} thumbnails in this run", processedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in ThumbnailBackfillWorker run loop");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
