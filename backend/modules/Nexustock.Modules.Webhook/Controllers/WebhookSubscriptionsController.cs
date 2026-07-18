using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Webhook.Contexts;
using Nexustock.Modules.Webhook.Entities;

namespace Nexustock.Modules.Webhook.Controllers;

[ApiController]
[Route("api/webhooks/subscriptions")]
public class WebhookSubscriptionsController : ControllerBase
{
    private readonly WebhookDbContext _db;

    public WebhookSubscriptionsController(WebhookDbContext db)
    {
        _db = db;
    }

    private Guid GetTenantId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "tenantId");
        return claim != null ? Guid.Parse(claim.Value) : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    /// <summary>Danh sách subscriptions của tenant (không trả secretKey).</summary>
    [HttpGet]
    public async Task<IActionResult> GetSubscriptions()
    {
        var tenantId = GetTenantId();
        var raw = await _db.WebhookSubscriptions
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.TargetUrl,
                s.EventTypes,
                s.IsActive,
                s.CreatedAt,
                s.UpdatedAt
            })
            .ToListAsync();

        var result = raw.Select(s => new
        {
            s.Id,
            s.TargetUrl,
            eventTypes = JsonSerializer.Deserialize<string[]>(s.EventTypes) ?? Array.Empty<string>(),
            s.IsActive,
            s.CreatedAt,
            s.UpdatedAt
        });

        return Ok(result);
    }

    /// <summary>Tạo subscription mới. secretKey trả về 1 lần duy nhất.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TargetUrl))
            return BadRequest(new { errorCode = "webhook.targetUrlRequired", message = "targetUrl là bắt buộc." });

        if (req.EventTypes == null || req.EventTypes.Length == 0)
            return BadRequest(new { errorCode = "webhook.eventTypesRequired", message = "eventTypes không được rỗng." });

        var tenantId = GetTenantId();
        var secretKey = GenerateSecretKey();
        var now = DateTime.UtcNow;

        var sub = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TargetUrl = req.TargetUrl,
            SecretKey = secretKey,
            EventTypes = JsonSerializer.Serialize(req.EventTypes),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.WebhookSubscriptions.Add(sub);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            subscriptionId = sub.Id,
            secretKey = secretKey  // Trả về bản rõ 1 lần duy nhất
        });
    }

    /// <summary>Cập nhật targetUrl, eventTypes hoặc isActive.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateSubscription(Guid id, [FromBody] UpdateSubscriptionRequest req)
    {
        var tenantId = GetTenantId();
        var sub = await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

        if (sub == null) return NotFound(new { errorCode = "webhook.subscriptionNotFound" });

        if (req.TargetUrl != null) sub.TargetUrl = req.TargetUrl;
        if (req.EventTypes != null) sub.EventTypes = JsonSerializer.Serialize(req.EventTypes);
        if (req.IsActive.HasValue) sub.IsActive = req.IsActive.Value;
        sub.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>Soft-delete subscription (isActive = false).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSubscription(Guid id)
    {
        var tenantId = GetTenantId();
        var sub = await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

        if (sub == null) return NotFound(new { errorCode = "webhook.subscriptionNotFound" });

        sub.IsActive = false;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    private static string GenerateSecretKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "whsec_" + Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

public class CreateSubscriptionRequest
{
    public string TargetUrl { get; set; } = null!;
    public string[] EventTypes { get; set; } = Array.Empty<string>();
}

public class UpdateSubscriptionRequest
{
    public string? TargetUrl { get; set; }
    public string[]? EventTypes { get; set; }
    public bool? IsActive { get; set; }
}
