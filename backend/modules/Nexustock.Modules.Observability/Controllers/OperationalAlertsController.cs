using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Entities;
using Nexustock.Modules.Observability.Services;

namespace Nexustock.Modules.Observability.Controllers;

[Authorize]
[ApiController]
[Route("api/observability/alerts")]
public class OperationalAlertsController : ControllerBase
{
    private readonly ObservabilityDbContext _db;
    private readonly IActivityTimelineService _timelineService;
    private readonly ITraceContext _traceContext;

    public OperationalAlertsController(
        ObservabilityDbContext db,
        IActivityTimelineService timelineService,
        ITraceContext traceContext)
    {
        _db = db;
        _timelineService = timelineService;
        _traceContext = traceContext;
    }

    private Guid GetTenantId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "tenantId");
        return claim != null ? Guid.Parse(claim.Value) : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    private Guid GetUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        return claim != null ? Guid.Parse(claim.Value) : Guid.Empty;
    }

    private string GetUserName()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        return claim != null ? claim.Value : "Admin";
    }

    /// <summary>
    /// Lấy danh sách alert với filter và phân trang.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] string? alertType,
        [FromQuery] string? sourceModule,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = GetTenantId();
        var query = _db.OperationalAlerts
            .Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrEmpty(severity)) query = query.Where(a => a.Severity == severity);
        if (!string.IsNullOrEmpty(alertType)) query = query.Where(a => a.AlertType == alertType);
        if (!string.IsNullOrEmpty(sourceModule)) query = query.Where(a => a.SourceModule == sourceModule);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>
    /// Acknowledge một alert.
    /// </summary>
    [HttpPost("{id:guid}/ack")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id, [FromBody] AckAlertRequest req)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userName = GetUserName();

        var alert = await _db.OperationalAlerts
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);

        if (alert == null)
            return NotFound(new { errorCode = "observability.alertNotFound" });

        if (alert.Status != "open")
            return BadRequest(new { errorCode = "observability.invalidAlertStatus", message = "Chỉ có thể ack alert ở trạng thái open." });

        var now = DateTime.UtcNow;
        alert.Status = "acknowledged";
        alert.AcknowledgedBy = userId;
        alert.AcknowledgedAt = now;
        alert.UpdatedAt = now;

        await _db.SaveChangesAsync();

        var traceId = _traceContext.GetCurrentTraceId();
        await _timelineService.RecordAsync(
            tenantId,
            "Alert",
            id,
            "alert.acknowledged",
            $"Cảnh báo {alert.Title} đã được xác nhận",
            $"Người xác nhận: {userName}. Ghi chú: {req.Note}",
            "info",
            traceId,
            new { note = req.Note, acknowledgedBy = userId },
            default);

        return Ok(new { success = true, status = alert.Status });
    }

    /// <summary>
    /// Resolve một alert.
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> ResolveAlert(Guid id, [FromBody] ResolveAlertRequest req)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userName = GetUserName();

        var alert = await _db.OperationalAlerts
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);

        if (alert == null)
            return NotFound(new { errorCode = "observability.alertNotFound" });

        if (alert.Status != "open" && alert.Status != "acknowledged")
            return BadRequest(new { errorCode = "observability.invalidAlertStatus", message = "Không thể giải quyết alert đã được resolve." });

        var now = DateTime.UtcNow;
        alert.Status = "resolved";
        alert.ResolvedBy = userId;
        alert.ResolvedAt = now;
        alert.UpdatedAt = now;

        await _db.SaveChangesAsync();

        var traceId = _traceContext.GetCurrentTraceId();
        await _timelineService.RecordAsync(
            tenantId,
            "Alert",
            id,
            "alert.resolved",
            $"Cảnh báo {alert.Title} đã được giải quyết",
            $"Người giải quyết: {userName}. Ghi chú: {req.Note}",
            "info",
            traceId,
            new { note = req.Note, resolvedBy = userId },
            default);

        return Ok(new { success = true, status = alert.Status });
    }

    /// <summary>
    /// Endpoint kiểm thử hỗ trợ tạo mock alert.
    /// </summary>
    [HttpPost("test-alert")]
    public async Task<IActionResult> TestAlert([FromBody] TestAlertRequest req)
    {
        var tenantId = GetTenantId();
        var alert = new OperationalAlert
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AlertType = req.AlertType,
            Severity = req.Severity,
            Status = "open",
            Title = req.Title,
            Message = req.Message,
            SourceModule = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.OperationalAlerts.Add(alert);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, alertId = alert.Id });
    }
}

public class AckAlertRequest
{
    public string? Note { get; set; }
}

public class ResolveAlertRequest
{
    public string? Note { get; set; }
}

public class TestAlertRequest
{
    public string AlertType { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
}
