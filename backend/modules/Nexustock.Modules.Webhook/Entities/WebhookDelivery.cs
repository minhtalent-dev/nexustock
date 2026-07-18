using System;

namespace Nexustock.Modules.Webhook.Entities;

/// <summary>
/// Bản ghi Outbox: một lần gửi Webhook tương ứng với một subscription.
/// </summary>
public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubscriptionId { get; set; }

    /// <summary>Loại sự kiện, ví dụ: inbound.completed, shipment.confirmed.</summary>
    public string EventType { get; set; } = null!;

    /// <summary>JSON payload gửi đến targetUrl.</summary>
    public string Payload { get; set; } = null!;

    /// <summary>pending | sending | delivered | deadLetter</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Số lần đã thử gửi.</summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>Thời điểm thử gửi tiếp theo.</summary>
    public DateTime NextAttemptAt { get; set; }

    /// <summary>Trace ID liên kết với business action.</summary>
    public string TraceId { get; set; } = null!;

    /// <summary>HTTP status code gần nhất từ target URL.</summary>
    public int? LastResponseCode { get; set; }

    /// <summary>Chi tiết lỗi kết nối gần nhất.</summary>
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public WebhookSubscription? Subscription { get; set; }
}
