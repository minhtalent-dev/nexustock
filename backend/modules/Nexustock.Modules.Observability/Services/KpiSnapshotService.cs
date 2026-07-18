using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Entities;
using Nexustock.Modules.Webhook.Contexts;

namespace Nexustock.Modules.Observability.Services;

public class KpiSnapshotService : IKpiSnapshotService
{
    private readonly ObservabilityDbContext _observabilityDb;
    private readonly WebhookDbContext _webhookDb;
    private readonly ExceptionsDbContext _exceptionsDb;
    private readonly InboundDbContext _inboundDb;
    private readonly InventoryDbContext _inventoryDb;

    public KpiSnapshotService(
        ObservabilityDbContext observabilityDb,
        WebhookDbContext webhookDb,
        ExceptionsDbContext exceptionsDb,
        InboundDbContext inboundDb,
        InventoryDbContext inventoryDb)
    {
        _observabilityDb = observabilityDb;
        _webhookDb = webhookDb;
        _exceptionsDb = exceptionsDb;
        _inboundDb = inboundDb;
        _inventoryDb = inventoryDb;
    }

    public async Task ComputeAndSaveKpisAsync(Guid tenantId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
    {
        // 1. Webhook Deliveries KPIs
        var webhooks = await _webhookDb.WebhookDeliveries
            .Where(d => d.TenantId == tenantId && d.CreatedAt >= periodStart && d.CreatedAt <= periodEnd)
            .ToListAsync(ct);

        var totalWebhooks = webhooks.Count;
        var successWebhooks = webhooks.Count(d => d.Status == "delivered");
        var dlqCount = webhooks.Count(d => d.Status == "deadLetter");
        var retryCount = webhooks.Sum(d => d.RetryCount);

        decimal successRate = 100m;
        if (totalWebhooks > 0)
        {
            successRate = Math.Round((decimal)successWebhooks / totalWebhooks * 100, 2);
        }

        // 2. Exception KPIs
        var openExceptions = await _exceptionsDb.OperationalExceptions
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.Status == "Open")
            .Select(e => e.CreatedAt)
            .ToListAsync(ct);

        var openExceptionsCount = openExceptions.Count;
        var avgAgingMinutes = 0m;
        if (openExceptionsCount > 0)
        {
            var now = DateTime.UtcNow;
            var totalMinutes = openExceptions.Sum(createdAt => (now - createdAt).TotalMinutes);
            avgAgingMinutes = Math.Round((decimal)(totalMinutes / openExceptionsCount), 2);
        }

        // 3. Inbound KPIs
        var completedInboundCount = await _inboundDb.InboundOrders
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && o.Status == InboundOrderStatus.Completed && o.CreatedAt >= periodStart && o.CreatedAt <= periodEnd)
            .CountAsync(ct);

        // 4. Outbound KPIs (Shipped Shipment status = Shipped or Completed)
        var shippedOutboundCount = await _inventoryDb.Shipments
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && (s.Status == "Shipped" || s.Status == "Completed") && s.CreatedAt >= periodStart && s.CreatedAt <= periodEnd)
            .CountAsync(ct);

        // 5. Inventory Adjustment KPIs (Applied adjustments)
        var adjustmentCount = await _inventoryDb.StockAdjustments
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Status == "Applied" && a.CreatedAt >= periodStart && a.CreatedAt <= periodEnd)
            .CountAsync(ct);

        // Save Snapshots
        var computedAt = DateTime.UtcNow;

        SaveSnapshot(tenantId, "webhook.deliverySuccessRate", "integration", successRate, "percent", periodStart, periodEnd, "Webhook", computedAt);
        SaveSnapshot(tenantId, "webhook.dlqCount", "integration", dlqCount, "count", periodStart, periodEnd, "Webhook", computedAt);
        SaveSnapshot(tenantId, "webhook.retryCount", "integration", retryCount, "count", periodStart, periodEnd, "Webhook", computedAt);
        SaveSnapshot(tenantId, "exception.openCount", "exception", openExceptionsCount, "count", periodStart, periodEnd, "Exceptions", computedAt);
        SaveSnapshot(tenantId, "exception.avgAgingMinutes", "exception", avgAgingMinutes, "minutes", periodStart, periodEnd, "Exceptions", computedAt);
        SaveSnapshot(tenantId, "inbound.completedCount", "warehouse", completedInboundCount, "count", periodStart, periodEnd, "Inbound", computedAt);
        SaveSnapshot(tenantId, "outbound.shippedCount", "warehouse", shippedOutboundCount, "count", periodStart, periodEnd, "Inventory", computedAt);
        SaveSnapshot(tenantId, "inventory.adjustmentCount", "inventory", adjustmentCount, "count", periodStart, periodEnd, "Inventory", computedAt);

        await _observabilityDb.SaveChangesAsync(ct);
    }

    private void SaveSnapshot(
        Guid tenantId,
        string metricKey,
        string metricGroup,
        decimal value,
        string unit,
        DateTime periodStart,
        DateTime periodEnd,
        string sourceModule,
        DateTime computedAt)
    {
        var snapshot = new KpiSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MetricKey = metricKey,
            MetricGroup = metricGroup,
            Value = value,
            Unit = unit,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            SourceModule = sourceModule,
            ComputedAt = computedAt
        };

        _observabilityDb.KpiSnapshots.Add(snapshot);
    }
}
