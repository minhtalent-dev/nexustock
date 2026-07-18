using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Services;

namespace Nexustock.Modules.Observability.Controllers;

[Authorize]
[ApiController]
[Route("api/observability")]
public class ObservabilityDashboardController : ControllerBase
{
    private readonly ObservabilityDbContext _db;
    private readonly ITraceContext _traceContext;

    public ObservabilityDashboardController(ObservabilityDbContext db, ITraceContext traceContext)
    {
        _db = db;
        _traceContext = traceContext;
    }

    private Guid GetTenantId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "tenantId");
        return claim != null ? Guid.Parse(claim.Value) : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    /// <summary>
    /// Lấy tổng quan các chỉ số KPI và số lượng alert đang hoạt động.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var tenantId = GetTenantId();
        var traceId = _traceContext.GetCurrentTraceId();

        var queryFrom = from ?? DateTime.UtcNow.Date;
        var queryTo = to ?? DateTime.UtcNow;

        var latestSnapshots = await _db.KpiSnapshots
            .Where(s => s.TenantId == tenantId && s.ComputedAt >= queryFrom && s.ComputedAt <= queryTo)
            .GroupBy(s => s.MetricKey)
            .Select(g => g.OrderByDescending(s => s.ComputedAt).First())
            .ToListAsync();

        var metricLabels = new Dictionary<string, string>
        {
            { "webhook.deliverySuccessRate", "Tỷ lệ webhook thành công" },
            { "webhook.dlqCount", "Số lượng webhook trong DLQ" },
            { "webhook.retryCount", "Tổng số lượt retry webhook" },
            { "exception.openCount", "Sự cố vận hành đang mở" },
            { "exception.avgAgingMinutes", "Thời gian xử lý sự cố TB" },
            { "inbound.completedCount", "Đơn nhập kho hoàn thành" },
            { "outbound.shippedCount", "Đơn xuất kho đã xuất" },
            { "inventory.adjustmentCount", "Số lần điều chỉnh tồn kho" }
        };

        var cards = latestSnapshots.Select(s => new
        {
            metricKey = s.MetricKey,
            label = metricLabels.TryGetValue(s.MetricKey, out var lbl) ? lbl : s.MetricKey,
            value = s.Value,
            unit = s.Unit,
            trend = "flat"
        }).ToList();

        foreach (var key in metricLabels.Keys)
        {
            if (!cards.Any(c => c.metricKey == key))
            {
                cards.Add(new
                {
                    metricKey = key,
                    label = metricLabels[key],
                    value = 0m,
                    unit = key.Contains("Rate") ? "percent" : key.Contains("Minutes") ? "minutes" : "count",
                    trend = "unavailable"
                });
            }
        }

        var activeAlertsCount = await _db.OperationalAlerts
            .CountAsync(a => a.TenantId == tenantId && a.Status == "open");

        return Ok(new
        {
            period = new { from = queryFrom, to = queryTo },
            cards,
            activeAlerts = activeAlertsCount,
            traceId
        });
    }

    /// <summary>
    /// Lấy lịch sử chụp ảnh KPI.
    /// </summary>
    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis(
        [FromQuery] string? metricGroup,
        [FromQuery] string? metricKey,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = GetTenantId();
        var query = _db.KpiSnapshots
            .Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrEmpty(metricGroup)) query = query.Where(s => s.MetricGroup == metricGroup);
        if (!string.IsNullOrEmpty(metricKey)) query = query.Where(s => s.MetricKey == metricKey);
        if (from.HasValue) query = query.Where(s => s.ComputedAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.ComputedAt <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.ComputedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>
    /// Endpoint kiểm thử hỗ trợ ghi log và test masking.
    /// </summary>
    [HttpPost("test-trace-log")]
    public async Task<IActionResult> TestTraceLog([FromBody] TestTraceLogRequest req)
    {
        var tenantId = GetTenantId();
        var log = new Nexustock.Modules.Observability.Entities.TraceLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TraceId = req.TraceId,
            SpanName = "TestSpan",
            Source = "test",
            Level = "info",
            Message = SensitiveDataMasker.Mask(req.Message),
            MetadataJson = SensitiveDataMasker.Mask(req.MetadataJson),
            CreatedAt = DateTime.UtcNow
        };

        _db.TraceLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

public class TestTraceLogRequest
{
    public string TraceId { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string MetadataJson { get; set; } = null!;
}
