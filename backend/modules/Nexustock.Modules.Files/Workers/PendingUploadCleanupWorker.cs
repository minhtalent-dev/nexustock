using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Files.Services;

namespace Nexustock.Modules.Files.Workers;

public sealed class PendingUploadCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingUploadCleanupWorker> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    public PendingUploadCleanupWorker(IServiceProvider serviceProvider, ILogger<PendingUploadCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay initial run to allow startup completion
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<IPendingUploadCleanupService>();
                await cleanupService.CleanupExpiredPendingUploadsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error during pending upload cleanup iteration");
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
