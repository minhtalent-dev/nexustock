using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Webhook.Contexts;
using Nexustock.Modules.Webhook.Entities;
using Nexustock.Modules.Webhook.Services;

namespace Nexustock.Modules.Webhook.Workers;

public class WebhookOutboxWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookOutboxWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // Backoff offsets theo retryCount (1-based)
    private static readonly int[] BackoffMinutes = { 1, 5, 15, 60 };
    private static readonly Random _rng = new();

    public WebhookOutboxWorker(
        IServiceProvider serviceProvider,
        ILogger<WebhookOutboxWorker> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startup recovery: reset "sending" về "pending" nếu worker bị crash trước đó
        await RecoverStuckSendingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WebhookOutbox] Lỗi trong vòng poll.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task RecoverStuckSendingAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
        var stuck = await db.WebhookDeliveries
            .Where(d => d.Status == "sending")
            .ToListAsync(ct);

        if (stuck.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var d in stuck)
        {
            d.Status = "pending";
            d.NextAttemptAt = now;
            d.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        _logger.LogWarning("[WebhookOutbox] Recovery: reset {Count} bản ghi 'sending' về 'pending'.", stuck.Count);
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
        var signer = scope.ServiceProvider.GetRequiredService<IWebhookSigningService>();

        var now = DateTime.UtcNow;

        // Lấy batch 50 pending deliveries đến hạn
        var deliveries = await db.WebhookDeliveries
            .Where(d => d.Status == "pending" && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt)
            .Take(50)
            .Include(d => d.Subscription)
            .ToListAsync(ct);

        if (deliveries.Count == 0) return;

        // Mark sending (optimistic lock: chỉ update những record vẫn còn pending)
        foreach (var d in deliveries)
        {
            d.Status = "sending";
            d.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);

        // Gửi từng delivery
        foreach (var delivery in deliveries)
        {
            if (delivery.Subscription == null || !delivery.Subscription.IsActive)
            {
                delivery.Status = "deadLetter";
                delivery.LastError = "Subscription không tồn tại hoặc đã bị vô hiệu.";
                delivery.UpdatedAt = DateTime.UtcNow;
                continue;
            }

            await SendDeliveryAsync(db, signer, delivery, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SendDeliveryAsync(
        WebhookDbContext db,
        IWebhookSigningService signer,
        WebhookDelivery delivery,
        CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = signer.ComputeSignature(delivery.Subscription!.SecretKey, timestamp, delivery.Payload);

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var request = new HttpRequestMessage(HttpMethod.Post, delivery.Subscription.TargetUrl)
            {
                Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Nexustock-Event", delivery.EventType);
            request.Headers.Add("X-Nexustock-Delivery-Id", delivery.Id.ToString());
            request.Headers.Add("X-Nexustock-Timestamp", timestamp);
            request.Headers.Add("X-Nexustock-Signature", $"sha256={signature}");

            var response = await client.SendAsync(request, ct);
            var statusCode = (int)response.StatusCode;

            _logger.LogInformation(
                "[WebhookOutbox] event={Event} url={Url} attempt={Attempt} status={Status} traceId={TraceId}",
                delivery.EventType, delivery.Subscription.TargetUrl, delivery.RetryCount + 1, statusCode, delivery.TraceId);

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = "delivered";
                delivery.LastResponseCode = statusCode;
                delivery.UpdatedAt = DateTime.UtcNow;
            }
            else if (statusCode == 429 || statusCode >= 500)
            {
                // Retry
                ApplyRetry(delivery, statusCode, null);
            }
            else
            {
                // 4xx không phải 429 → permanent failure
                delivery.Status = "deadLetter";
                delivery.LastResponseCode = statusCode;
                delivery.LastError = $"HTTP {statusCode} - permanent failure.";
                delivery.UpdatedAt = DateTime.UtcNow;
                _logger.LogWarning("[WebhookOutbox] DeadLetter (permanent): event={Event} url={Url} status={Status} traceId={TraceId}",
                    delivery.EventType, delivery.Subscription.TargetUrl, statusCode, delivery.TraceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebhookOutbox] Lỗi kết nối: event={Event} url={Url} traceId={TraceId}",
                delivery.EventType, delivery.Subscription.TargetUrl, delivery.TraceId);
            ApplyRetry(delivery, null, ex.Message);
        }
    }

    private static void ApplyRetry(WebhookDelivery delivery, int? responseCode, string? error)
    {
        delivery.RetryCount += 1;
        delivery.LastResponseCode = responseCode;
        delivery.LastError = error;
        delivery.UpdatedAt = DateTime.UtcNow;

        if (delivery.RetryCount >= 5)
        {
            delivery.Status = "deadLetter";
            return;
        }

        // Backoff: index = retryCount - 1 (0-based)
        var idx = Math.Min(delivery.RetryCount - 1, BackoffMinutes.Length - 1);
        var jitterSeconds = _rng.Next(0, 30);
        delivery.Status = "pending";
        delivery.NextAttemptAt = DateTime.UtcNow
            .AddMinutes(BackoffMinutes[idx])
            .AddSeconds(jitterSeconds);
    }
}
