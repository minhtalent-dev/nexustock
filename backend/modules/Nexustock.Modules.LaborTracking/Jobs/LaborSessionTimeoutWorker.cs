using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.LaborTracking.Contexts;
using Nexustock.Modules.LaborTracking.Entities;

namespace Nexustock.Modules.LaborTracking.Jobs;

public class LaborSessionTimeoutWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LaborSessionTimeoutWorker> _logger;

    public LaborSessionTimeoutWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<LaborSessionTimeoutWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enableJob = _configuration.GetValue<bool>("LaborTracking:EnableTimeoutWorker", true);
        if (!enableJob)
        {
            _logger.LogInformation("[LaborSessionTimeoutWorker] Bị tắt bởi cấu hình.");
            return;
        }

        var intervalSec = _configuration.GetValue<int>("LaborTracking:TimeoutWorkerIntervalSeconds", 60);
        var batchSize = _configuration.GetValue<int>("LaborTracking:TimeoutBatchSize", 100);

        _logger.LogInformation("[LaborSessionTimeoutWorker] Đang chạy với chu kỳ {Interval} giây, batch size {BatchSize}...", intervalSec, batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LaborTrackingDbContext>();

                var now = DateTimeOffset.UtcNow;

                var timeoutSessions = await dbContext.LaborSessions
                    .Where(s =>
                        (s.Status == "Running" || s.Status == "Paused") &&
                        s.TimeoutAt.HasValue &&
                        s.TimeoutAt <= now)
                    .Take(batchSize)
                    .ToListAsync(stoppingToken);

                if (timeoutSessions.Count > 0)
                {
                    using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

                    foreach (var session in timeoutSessions)
                    {
                        var originalStatus = session.Status;
                        var timeoutAt = session.TimeoutAt ?? now;
                        session.Status = "TimedOut";
                        session.CompletedAt = timeoutAt;
                        session.UpdatedAt = now;
                        session.UpdatedBy = "LaborSessionTimeoutWorker";

                        // Nếu đang paused, tính toán phần pausedSeconds cho đến lúc timeoutAt
                        if (originalStatus == "Paused" && session.LastPausedAt != null)
                        {
                            var delta = (int)(timeoutAt - session.LastPausedAt.Value).TotalSeconds;
                            if (delta >= 0) session.PausedSeconds += delta;
                            session.LastPausedAt = null;
                        }

                        var totalSeconds = (int)(timeoutAt - session.StartedAt).TotalSeconds;
                        var duration = totalSeconds - session.PausedSeconds;
                        session.DurationSeconds = Math.Max(0, duration);

                        // Thêm event TimedOut
                        var timeoutEvent = new LaborSessionEvent
                        {
                            Id = Guid.NewGuid(),
                            TenantId = session.TenantId,
                            SessionId = session.Id,
                            EventType = "TimedOut",
                            Actor = "system",
                            OccurredAt = now,
                            Payload = System.Text.Json.JsonSerializer.Serialize(new {
                                reason = "SLA threshold exceeded",
                                timeoutAt
                            })
                        };
                        dbContext.LaborSessionEvents.Add(timeoutEvent);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
                    _logger.LogInformation("[LaborSessionTimeoutWorker] Đã tự động đánh dấu TimedOut cho {Count} sessions quá hạn.", timeoutSessions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LaborSessionTimeoutWorker] Lỗi trong vòng lặp xử lý timeout labor sessions.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSec), stoppingToken);
        }
    }
}
