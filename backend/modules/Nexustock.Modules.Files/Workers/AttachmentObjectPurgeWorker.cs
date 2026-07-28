using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Services;

namespace Nexustock.Modules.Files.Workers;

public sealed class AttachmentObjectPurgeWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ThumbnailOptions _options;
    private readonly ILogger<AttachmentObjectPurgeWorker> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public AttachmentObjectPurgeWorker(
        IServiceProvider serviceProvider,
        IOptions<ThumbnailOptions> options,
        ILogger<AttachmentObjectPurgeWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startup delay để tránh tranh chấp tài nguyên lúc khởi động
        var delaySeconds = Math.Max(10, _options.StartupDelaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

        _logger.LogInformation("AttachmentObjectPurgeWorker started. CheckInterval={Interval}", CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                var purgeService = scope.ServiceProvider.GetRequiredService<IAttachmentObjectPurgeService>();

                // Lấy 50 attachment đã soft-deleted (DeletedAt != null) nhưng chưa được dọn dẹp objects (ObjectsPurgedAt == null)
                var pendingPurges = await db.FileAttachments
                    .IgnoreQueryFilters()
                    .Where(a => a.DeletedAt != null && a.ObjectsPurgedAt == null)
                    .OrderBy(a => a.Id)
                    .Take(_options.BatchSize)
                    .ToListAsync(stoppingToken);

                if (pendingPurges.Count > 0)
                {
                    _logger.LogInformation("Found {Count} deleted attachments pending storage object purge", pendingPurges.Count);

                    foreach (var attachment in pendingPurges)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        try
                        {
                            await purgeService.PurgeAttachmentFilesAsync(attachment.Id, stoppingToken);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogWarning(ex, "Failed to purge storage files for attachment {Id} in background run", attachment.Id);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in AttachmentObjectPurgeWorker run loop");
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
