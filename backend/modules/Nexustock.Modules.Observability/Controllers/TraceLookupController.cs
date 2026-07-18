using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Webhook.Contexts;

namespace Nexustock.Modules.Observability.Controllers;

[Authorize]
[ApiController]
[Route("api/observability/traces")]
public class TraceLookupController : ControllerBase
{
    private readonly ObservabilityDbContext _obsDb;
    private readonly WebhookDbContext _webhookDb;

    public TraceLookupController(ObservabilityDbContext obsDb, WebhookDbContext webhookDb)
    {
        _obsDb = obsDb;
        _webhookDb = webhookDb;
    }

    private Guid GetTenantId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "tenantId");
        return claim != null ? Guid.Parse(claim.Value) : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    /// <summary>
    /// Tra cứu chi tiết một Trace ID bao gồm trace logs, timeline và webhook deliveries.
    /// </summary>
    [HttpGet("{traceId}")]
    public async Task<IActionResult> GetTraceDetail(string traceId)
    {
        var tenantId = GetTenantId();

        var traceLogs = await _obsDb.TraceLogs
            .Where(t => t.TraceId == traceId && (t.TenantId == null || t.TenantId == tenantId))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var timelineEntries = await _obsDb.ActivityTimelineEntries
            .Where(t => t.TraceId == traceId && t.TenantId == tenantId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var webhookDeliveries = await _webhookDb.WebhookDeliveries
            .Where(d => d.TraceId == traceId && d.TenantId == tenantId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        return Ok(new
        {
            traceId,
            traceLogs,
            timelineEntries,
            webhookDeliveries
        });
    }
}
