using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nexustock.Modules.Webhook.Services;

/// <summary>
/// Enqueue một Webhook delivery vào Outbox (WebhookDeliveries).
/// Phải được gọi trong cùng ambient TransactionScope với business action.
/// </summary>
public interface IWebhookOutboxService
{
    Task EnqueueAsync(Guid tenantId, string eventType, string payloadJson, string traceId, CancellationToken ct = default);
}
