using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Webhook.Contexts;

namespace Nexustock.Modules.Observability.Services;

public class KpiSnapshotJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KpiSnapshotJob> _logger;

    public KpiSnapshotJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<KpiSnapshotJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enableJob = _configuration.GetValue<bool>("Observability:EnableKpiSnapshotJob", true);
        if (!enableJob)
        {
            _logger.LogInformation("[KpiSnapshotJob] Bị tắt bởi cấu hình.");
            return;
        }

        var intervalSec = _configuration.GetValue<int>("Observability:KpiSnapshotIntervalSeconds", 300);
        _logger.LogInformation("[KpiSnapshotJob] Đang chạy với chu kỳ {Interval} giây...", intervalSec);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var kpiService = scope.ServiceProvider.GetRequiredService<IKpiSnapshotService>();
                var webhookDb = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

                // Lấy danh sách tenant có đăng ký webhook
                var tenantIds = await webhookDb.WebhookSubscriptions
                    .Select(s => s.TenantId)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                // Luôn bao gồm tenant mặc định
                var defaultTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
                if (!tenantIds.Contains(defaultTenant))
                {
                    tenantIds.Add(defaultTenant);
                }

                var now = DateTime.UtcNow;
                var periodStart = now.Date; // KPI từ đầu ngày hiện tại
                var periodEnd = now;

                foreach (var tenantId in tenantIds)
                {
                    await kpiService.ComputeAndSaveKpisAsync(tenantId, periodStart, periodEnd, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KpiSnapshotJob] Lỗi trong vòng lặp tính toán KPI.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSec), stoppingToken);
        }
    }
}
