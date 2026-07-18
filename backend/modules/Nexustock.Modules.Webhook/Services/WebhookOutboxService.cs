using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Webhook.Contexts;
using Nexustock.Modules.Webhook.Entities;

namespace Nexustock.Modules.Webhook.Services;

public class WebhookOutboxService : IWebhookOutboxService
{
    private readonly WebhookDbContext _db;

    public WebhookOutboxService(WebhookDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Tìm tất cả active subscriptions khớp eventType và tạo WebhookDelivery pending.
    /// Phải được gọi trong cùng ambient TransactionScope với business transaction để đảm bảo atomic.
    /// </summary>
    public async Task EnqueueAsync(Guid tenantId, string eventType, string payloadJson, string traceId, CancellationToken ct = default)
    {
        var subscriptions = await _db.WebhookSubscriptions
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .ToListAsync(ct);

        var matched = subscriptions
            .Where(s => SubscriptionMatchesEvent(s.EventTypes, eventType))
            .ToList();

        if (matched.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var sub in matched)
        {
            _db.WebhookDeliveries.Add(new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SubscriptionId = sub.Id,
                EventType = eventType,
                Payload = payloadJson,
                Status = "pending",
                RetryCount = 0,
                NextAttemptAt = now,
                TraceId = traceId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Kiểm tra eventType có khớp với JSON array trong EventTypes của subscription.
    /// </summary>
    private static bool SubscriptionMatchesEvent(string eventTypesJson, string eventType)
    {
        try
        {
            var list = JsonSerializer.Deserialize<string[]>(eventTypesJson);
            if (list == null) return false;
            return list.Any(e =>
                string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase) ||
                (e.EndsWith(".*") && eventType.StartsWith(e[..^2], StringComparison.OrdinalIgnoreCase)));
        }
        catch
        {
            return false;
        }
    }
}
