using System;

namespace Nexustock.Modules.Webhook.Entities;

/// <summary>
/// Đăng ký nhận tin Webhook của một Tenant.
/// </summary>
public class WebhookSubscription
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>URL nhận webhook.</summary>
    public string TargetUrl { get; set; } = null!;

    /// <summary>
    /// Secret key dùng để ký HMAC-SHA256. Lưu bản rõ nội bộ.
    /// Chỉ trả về cho client 1 lần khi tạo, không expose sau đó.
    /// </summary>
    public string SecretKey { get; set; } = null!;

    /// <summary>
    /// Danh sách event type đăng ký dạng JSON array, ví dụ: ["shipment.confirmed","inbound.completed"].
    /// </summary>
    public string EventTypes { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
