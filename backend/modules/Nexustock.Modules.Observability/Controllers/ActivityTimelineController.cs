using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Observability.Contexts;

namespace Nexustock.Modules.Observability.Controllers;

[Authorize]
[ApiController]
[Route("api/observability/timeline")]
public class ActivityTimelineController : ControllerBase
{
    private readonly ObservabilityDbContext _db;

    public ActivityTimelineController(ObservabilityDbContext db)
    {
        _db = db;
    }

    private Guid GetTenantId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "tenantId");
        return claim != null ? Guid.Parse(claim.Value) : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    /// <summary>
    /// Lấy danh sách timeline hoạt động với phân trang và filter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] string? traceId,
        [FromQuery] string? severity,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = GetTenantId();
        var query = _db.ActivityTimelineEntries
            .Where(t => t.TenantId == tenantId);

        if (!string.IsNullOrEmpty(entityType)) query = query.Where(t => t.EntityType == entityType);
        if (entityId.HasValue) query = query.Where(t => t.EntityId == entityId.Value);
        if (!string.IsNullOrEmpty(traceId)) query = query.Where(t => t.TraceId == traceId);
        if (!string.IsNullOrEmpty(severity)) query = query.Where(t => t.Severity == severity);
        if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(t => t.CreatedAt <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>
    /// Lấy timeline của một thực thể nghiệp vụ cụ thể.
    /// </summary>
    [HttpGet("{entityType}/{entityId:guid}")]
    public async Task<IActionResult> GetEntityTimeline(string entityType, Guid entityId)
    {
        var tenantId = GetTenantId();
        var items = await _db.ActivityTimelineEntries
            .Where(t => t.TenantId == tenantId && t.EntityType == entityType && t.EntityId == entityId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(items);
    }
}
