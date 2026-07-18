using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Webhook.Contexts;

namespace Nexustock.Modules.Webhook.Controllers;

[ApiController]
[Route("api/webhooks/deliveries")]
public class WebhookDeliveriesController : ControllerBase
{
    private readonly WebhookDbContext _db;

    public WebhookDeliveriesController(WebhookDbContext db)
    {
        _db = db;
    }

    private Guid GetTenantId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "tenantId");
        return claim != null ? Guid.Parse(claim.Value) : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    /// <summary>Danh sách deliveries với filter và phân trang.</summary>
    [HttpGet]
    public async Task<IActionResult> GetDeliveries(
        [FromQuery] string? status,
        [FromQuery] Guid? subscriptionId,
        [FromQuery] string? eventType,
        [FromQuery] string? traceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = GetTenantId();
        var query = _db.WebhookDeliveries
            .Where(d => d.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(d => d.Status == status);
        if (subscriptionId.HasValue) query = query.Where(d => d.SubscriptionId == subscriptionId);
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(d => d.EventType == eventType);
        if (!string.IsNullOrWhiteSpace(traceId)) query = query.Where(d => d.TraceId.Contains(traceId));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.SubscriptionId,
                d.EventType,
                d.Status,
                d.RetryCount,
                d.NextAttemptAt,
                d.TraceId,
                d.LastResponseCode,
                d.LastError,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Replay một delivery từ deadLetter về pending.</summary>
    [HttpPost("{id:guid}/replay")]
    public async Task<IActionResult> ReplayDelivery(Guid id)
    {
        var tenantId = GetTenantId();
        var delivery = await _db.WebhookDeliveries
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (delivery == null)
            return NotFound(new { errorCode = "webhook.deliveryNotFound" });

        if (delivery.Status != "deadLetter")
            return BadRequest(new { errorCode = "webhook.notDeadLetter", message = "Chỉ có thể replay delivery ở trạng thái deadLetter." });

        var now = DateTime.UtcNow;
        delivery.Status = "pending";
        delivery.RetryCount = 0;
        delivery.NextAttemptAt = now;
        delivery.LastError = null;
        delivery.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return Ok(new { success = true, status = "pending", nextAttemptAt = delivery.NextAttemptAt });
    }

    /// <summary>Replay nhiều deliveries theo ids hoặc filterStatus.</summary>
    [HttpPost("replay-bulk")]
    public async Task<IActionResult> ReplayBulk([FromBody] ReplayBulkRequest req)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;
        int count = 0;

        if (req.Ids != null && req.Ids.Length > 0)
        {
            var deliveries = await _db.WebhookDeliveries
                .Where(d => d.TenantId == tenantId && req.Ids.Contains(d.Id) && d.Status == "deadLetter")
                .ToListAsync();

            foreach (var d in deliveries)
            {
                d.Status = "pending";
                d.RetryCount = 0;
                d.NextAttemptAt = now;
                d.LastError = null;
                d.UpdatedAt = now;
                count++;
            }
        }
        else if (!string.IsNullOrWhiteSpace(req.FilterStatus))
        {
            var deliveries = await _db.WebhookDeliveries
                .Where(d => d.TenantId == tenantId && d.Status == req.FilterStatus)
                .ToListAsync();

            foreach (var d in deliveries)
            {
                d.Status = "pending";
                d.RetryCount = 0;
                d.NextAttemptAt = now;
                d.LastError = null;
                d.UpdatedAt = now;
                count++;
            }
        }
        else
        {
            return BadRequest(new { errorCode = "webhook.invalidReplayRequest", message = "Phải cung cấp ids hoặc filterStatus." });
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true, replayed = count });
    }
}

public class ReplayBulkRequest
{
    public Guid[]? Ids { get; set; }
    public string? FilterStatus { get; set; }
}
