using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Entities;
using Nexustock.Modules.Webhook.Contexts;

namespace Nexustock.Modules.Observability.Services;

public class OperationalAlertEvaluatorJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OperationalAlertEvaluatorJob> _logger;

    public OperationalAlertEvaluatorJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OperationalAlertEvaluatorJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enableJob = _configuration.GetValue<bool>("Observability:EnableAlertEvaluatorJob", true);
        if (!enableJob)
        {
            _logger.LogInformation("[OperationalAlertEvaluatorJob] Bị tắt bởi cấu hình.");
            return;
        }

        var intervalSec = _configuration.GetValue<int>("Observability:AlertEvaluatorIntervalSeconds", 60);
        _logger.LogInformation("[OperationalAlertEvaluatorJob] Đang chạy với chu kỳ {Interval} giây...", intervalSec);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var observabilityDb = scope.ServiceProvider.GetRequiredService<ObservabilityDbContext>();
                var webhookDb = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
                var exceptionsDb = scope.ServiceProvider.GetRequiredService<ExceptionsDbContext>();

                // Lấy danh sách tenant có đăng ký webhook
                var tenantIds = await webhookDb.WebhookSubscriptions
                    .Select(s => s.TenantId)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                var defaultTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
                if (!tenantIds.Contains(defaultTenant))
                {
                    tenantIds.Add(defaultTenant);
                }

                foreach (var tenantId in tenantIds)
                {
                    await EvaluateAlertsForTenantAsync(tenantId, observabilityDb, webhookDb, exceptionsDb, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OperationalAlertEvaluatorJob] Lỗi trong vòng lặp đánh giá cảnh báo.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSec), stoppingToken);
        }
    }

    private async Task EvaluateAlertsForTenantAsync(
        Guid tenantId,
        ObservabilityDbContext obsDb,
        WebhookDbContext webhookDb,
        ExceptionsDbContext exceptionsDb,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // 1. Rule: webhook.dlqThreshold (DLQ count > 10 trong 1 giờ)
        var oneHourAgo = now.AddHours(-1);
        var dlqCount = await webhookDb.WebhookDeliveries
            .Where(d => d.TenantId == tenantId && d.Status == "deadLetter" && d.UpdatedAt >= oneHourAgo)
            .CountAsync(ct);

        if (dlqCount > 10)
        {
            await CreateOrUpdateAlertAsync(
                obsDb,
                tenantId,
                "webhook.dlqThreshold",
                "critical",
                "Webhook DLQ Threshold Exceeded",
                $"Phát hiện {dlqCount} webhook deliveries bị đưa vào DLQ trong 1 giờ qua (vượt ngưỡng 10).",
                "Webhook",
                null,
                null,
                dlqCount,
                10,
                ct);
        }

        // 2. Rule: webhook.retrySpike (retryCount tăng > 30 trong 15 phút)
        var fifteenMinsAgo = now.AddMinutes(-15);
        var retrySum = await webhookDb.WebhookDeliveries
            .Where(d => d.TenantId == tenantId && d.UpdatedAt >= fifteenMinsAgo)
            .SumAsync(d => d.RetryCount, ct);

        if (retrySum > 30)
        {
            await CreateOrUpdateAlertAsync(
                obsDb,
                tenantId,
                "webhook.retrySpike",
                "warning",
                "Webhook Retry Spike Detected",
                $"Tổng số lượt retry của webhook trong 15 phút qua là {retrySum} (vượt ngưỡng 30).",
                "Webhook",
                null,
                null,
                retrySum,
                30,
                ct);
        }

        // 3. Rule: kpi.stale (không có snapshot mới > 15 phút)
        var fifteenMinsAgoForKpi = now.AddMinutes(-15);
        var hasRecentSnapshot = await obsDb.KpiSnapshots
            .AnyAsync(s => s.TenantId == tenantId && s.ComputedAt >= fifteenMinsAgoForKpi, ct);

        if (!hasRecentSnapshot)
        {
            await CreateOrUpdateAlertAsync(
                obsDb,
                tenantId,
                "kpi.stale",
                "warning",
                "KPI Snapshot Stale",
                "Không phát hiện bản ghi KPI snapshot mới nào được tạo trong 15 phút qua.",
                "Observability",
                null,
                null,
                0,
                1,
                ct);
        }

        // 4. Rule: exception.aging (exception open quá 24 giờ)
        var twentyFourHoursAgo = now.AddHours(-24);
        var agingExceptions = await exceptionsDb.OperationalExceptions
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.Status == "Open" && e.CreatedAt <= twentyFourHoursAgo)
            .ToListAsync(ct);

        foreach (var ex in agingExceptions)
        {
            var hoursOpen = (int)(now - ex.CreatedAt).TotalHours;
            await CreateOrUpdateAlertAsync(
                obsDb,
                tenantId,
                "exception.aging",
                "warning",
                $"Exception Aging: {ex.Code}",
                $"Sự cố vận hành mã {ex.Code} (Severity: {ex.Severity}) đã tồn tại ở trạng thái Open trong {hoursOpen} giờ.",
                "Exceptions",
                "Exception",
                ex.Id,
                hoursOpen,
                24,
                ct);
        }

        await obsDb.SaveChangesAsync(ct);
    }

    private async Task CreateOrUpdateAlertAsync(
        ObservabilityDbContext obsDb,
        Guid tenantId,
        string alertType,
        string severity,
        string title,
        string message,
        string sourceModule,
        string? sourceEntityType,
        Guid? sourceEntityId,
        decimal metricValue,
        decimal thresholdValue,
        CancellationToken ct)
    {
        // Kiểm tra xem alert tương tự đang Open hay không
        var query = obsDb.OperationalAlerts
            .Where(a => a.TenantId == tenantId && a.AlertType == alertType && a.Status == "open");

        if (sourceEntityId.HasValue)
        {
            query = query.Where(a => a.SourceEntityId == sourceEntityId);
        }

        var existingAlert = await query.FirstOrDefaultAsync(ct);

        if (existingAlert != null)
        {
            // Update existing open alert
            existingAlert.MetricValue = metricValue;
            existingAlert.Message = message;
            existingAlert.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new alert
            var alert = new OperationalAlert
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AlertType = alertType,
                Severity = severity,
                Status = "open",
                Title = title,
                Message = message,
                SourceModule = sourceModule,
                SourceEntityType = sourceEntityType,
                SourceEntityId = sourceEntityId,
                MetricValue = metricValue,
                ThresholdValue = thresholdValue,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            obsDb.OperationalAlerts.Add(alert);
        }
    }
}
