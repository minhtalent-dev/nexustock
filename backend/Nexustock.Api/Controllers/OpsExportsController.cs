using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Rma.Contexts;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.Wave.Contexts;
using Nexustock.Modules.Putaway.Contexts;
using Nexustock.Modules.CrossDocking.Contexts;
using Nexustock.Modules.Replenishment.Contexts;
using Nexustock.Modules.MasterData.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nexustock.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/ops/exports")]
public class OpsExportsController : ControllerBase
{
    private const int MaxRows = 5000;
    private readonly InboundDbContext _inboundDb;
    private readonly InventoryDbContext _inventoryDb;
    private readonly RmaDbContext _rmaDb;
    private readonly ExceptionsDbContext _exceptionsDb;
    private readonly LpnDbContext _lpnDb;
    private readonly WaveDbContext _waveDb;
    private readonly PutawayDbContext _putawayDb;
    private readonly CrossDockingDbContext _crossDockingDb;
    private readonly ReplenishmentDbContext _replenishmentDb;
    private readonly IUserPermissionService _permissionService;

    public OpsExportsController(
        InboundDbContext inboundDb,
        InventoryDbContext inventoryDb,
        RmaDbContext rmaDb,
        ExceptionsDbContext exceptionsDb,
        LpnDbContext lpnDb,
        WaveDbContext waveDb,
        PutawayDbContext putawayDb,
        CrossDockingDbContext crossDockingDb,
        ReplenishmentDbContext replenishmentDb,
        IUserPermissionService permissionService)
    {
        _inboundDb = inboundDb;
        _inventoryDb = inventoryDb;
        _rmaDb = rmaDb;
        _exceptionsDb = exceptionsDb;
        _lpnDb = lpnDb;
        _waveDb = waveDb;
        _putawayDb = putawayDb;
        _crossDockingDb = crossDockingDb;
        _replenishmentDb = replenishmentDb;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] string type, [FromQuery] string format = "csv")
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || !await _permissionService.HasPermissionAsync(userId, "ops.export"))
        {
            return Forbid();
        }

        var tenantIdClaim = User.FindFirst("tenantId")?.Value ?? User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Forbid();
        }

        var t = (type ?? "").Trim().ToUpperInvariant();
        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        if (fmt is not ("csv" or "xlsx"))
            return BadRequest(new { error = "EXPORT_TYPE_INVALID", message = "format must be csv or xlsx" });

        List<string[]> rows;
        string fileBase;
        bool isTruncated;
        try
        {
            (rows, fileBase, isTruncated) = t switch
            {
                "INBOUND_ORDERS" => await BuildInboundOrdersAsync(tenantId),
                "SHIPMENTS" => await BuildShipmentsAsync(tenantId),
                "STOCKTAKES" => await BuildStocktakesAsync(tenantId),
                "RMA" => await BuildRmaAsync(tenantId),
                "LOTS" => await BuildLotsAsync(tenantId),
                "EXCEPTIONS" => await BuildExceptionsAsync(tenantId),
                "LPNS" => await BuildLpnsAsync(tenantId),
                "INVENTORY_BALANCES" => await BuildInventoryBalancesAsync(tenantId),
                "WAVES" => await BuildWavesAsync(tenantId),
                "PUTAWAY_PROPOSALS" => await BuildPutawayProposalsAsync(tenantId),
                "CROSS_DOCK_CANDIDATES" => await BuildCrossDockCandidatesAsync(tenantId),
                "REPLENISHMENT_TASKS" => await BuildReplenishmentTasksAsync(tenantId),
                _ => throw new ArgumentException("EXPORT_TYPE_INVALID")
            };
        }
        catch (ArgumentException)
        {
            return BadRequest(new { error = "EXPORT_TYPE_INVALID" });
        }

        Response.Headers["X-Export-Truncated"] = isTruncated ? "true" : "false";

        var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{fileBase}_{timeStamp}";

        if (fmt == "xlsx")
        {
            var bytes = SpreadsheetReader.WriteXlsx(rows);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
        }

        var csv = SpreadsheetReader.RowsToCsv(rows);
        var bom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bom, "text/csv", $"{fileName}.csv");
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildInboundOrdersAsync(Guid tenantId)
    {
        var items = await _inboundDb.InboundOrders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.OrderNo)
            .ThenBy(o => o.Id)
            .Select(o => new { o.OrderNo, o.Status, o.PartnerId, o.CreatedAt, o.CreatedBy })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "orderNo", "status", "partnerId", "createdAt", "createdBy" }
        };

        rows.AddRange(data.Select(o => new[]
        {
            o.OrderNo,
            o.Status.ToString(),
            o.PartnerId.ToString(),
            o.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            o.CreatedBy ?? ""
        }));

        return (rows, "inbound_orders", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildShipmentsAsync(Guid tenantId)
    {
        var items = await _inventoryDb.Shipments
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.ShipmentNo)
            .ThenBy(s => s.Id)
            .Select(s => new { s.ShipmentNo, s.Status, s.PartnerId, s.CreatedAt, s.CreatedBy })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "shipmentNo", "status", "partnerId", "createdAt", "createdBy" }
        };

        rows.AddRange(data.Select(s => new[]
        {
            s.ShipmentNo,
            s.Status,
            s.PartnerId.ToString(),
            s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            s.CreatedBy
        }));

        return (rows, "shipments", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildStocktakesAsync(Guid tenantId)
    {
        var items = await _inventoryDb.Stocktakes
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.StocktakeNo)
            .ThenBy(s => s.Id)
            .Select(s => new { s.StocktakeNo, s.Status, s.TotalVarianceAmount, s.CreatedAt, s.CreatedBy })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "stocktakeNo", "status", "totalVarianceAmount", "createdAt", "createdBy" }
        };

        rows.AddRange(data.Select(s => new[]
        {
            s.StocktakeNo,
            s.Status,
            s.TotalVarianceAmount.ToString("0.####"),
            s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            s.CreatedBy
        }));

        return (rows, "stocktakes", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildRmaAsync(Guid tenantId)
    {
        var items = await _rmaDb.RmaRequests
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.RmaNo)
            .ThenBy(r => r.Id)
            .Select(r => new { r.RmaNo, r.Status, r.CustomerId, r.ReferenceNo, r.CreatedAt, r.CreatedBy })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "rmaNo", "status", "customerId", "referenceNo", "createdAt", "createdBy" }
        };

        rows.AddRange(data.Select(r => new[]
        {
            r.RmaNo,
            r.Status,
            r.CustomerId.ToString(),
            r.ReferenceNo ?? "",
            r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            r.CreatedBy
        }));

        return (rows, "rma_requests", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildLotsAsync(Guid tenantId)
    {
        var items = await _inboundDb.Lots
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .OrderBy(l => l.LotNo)
            .ThenBy(l => l.Id)
            .Select(l => new { l.LotNo, l.ItemId, l.QcStatus, l.ExpiryDate, l.ProductionDate })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "lotNo", "itemId", "qcStatus", "expiryDate", "productionDate" }
        };

        rows.AddRange(data.Select(l => new[]
        {
            l.LotNo,
            l.ItemId.ToString(),
            l.QcStatus.ToString(),
            l.ExpiryDate?.ToString("yyyy-MM-dd") ?? "",
            l.ProductionDate?.ToString("yyyy-MM-dd") ?? ""
        }));

        return (rows, "lots", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildExceptionsAsync(Guid tenantId)
    {
        var items = await _exceptionsDb.OperationalExceptions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Code)
            .ThenBy(e => e.Id)
            .Select(e => new { e.Code, e.Type, e.Severity, e.Status, e.ReferenceType, e.ReferenceId, e.LocationId, e.LotNo, e.Qty, e.ReasonCode, e.Note, e.CreatedAt })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "code", "type", "severity", "status", "referenceType", "referenceId", "locationId", "lotNo", "qty", "reasonCode", "note", "createdAt" }
        };

        rows.AddRange(data.Select(e => new[]
        {
            e.Code,
            e.Type,
            e.Severity,
            e.Status,
            e.ReferenceType,
            e.ReferenceId.ToString(),
            e.LocationId?.ToString() ?? "",
            e.LotNo ?? "",
            e.Qty.ToString("0.####"),
            e.ReasonCode,
            e.Note ?? "",
            e.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }));

        return (rows, "exceptions", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildLpnsAsync(Guid tenantId)
    {
        var items = await _lpnDb.Lpns
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .OrderBy(l => l.LpnNo)
            .ThenBy(l => l.Id)
            .Select(l => new { l.LpnNo, l.LocationId, l.Status, l.CreatedAt, l.CreatedBy, l.UpdatedAt, l.UpdatedBy })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "lpnNo", "locationId", "status", "createdAt", "createdBy", "updatedAt", "updatedBy" }
        };

        rows.AddRange(data.Select(l => new[]
        {
            l.LpnNo,
            l.LocationId.ToString(),
            l.Status,
            l.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            l.CreatedBy,
            l.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
            l.UpdatedBy ?? ""
        }));

        return (rows, "lpns", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildInventoryBalancesAsync(Guid tenantId)
    {
        var items = await _inventoryDb.Inventories
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .OrderBy(i => i.ItemId)
            .ThenBy(i => i.Id)
            .Select(i => new { i.ItemId, i.LotNo, i.LocationId, i.QtyOnHand, i.QtyReserved, i.QtyAvailable, i.LpnId, i.CreatedAt, i.UpdatedAt })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "itemId", "lotNo", "locationId", "qtyOnHand", "qtyReserved", "qtyAvailable", "lpnId", "createdAt", "updatedAt" }
        };

        rows.AddRange(data.Select(i => new[]
        {
            i.ItemId.ToString(),
            i.LotNo,
            i.LocationId.ToString(),
            i.QtyOnHand.ToString("0.####"),
            i.QtyReserved.ToString("0.####"),
            i.QtyAvailable.ToString("0.####"),
            i.LpnId?.ToString() ?? "",
            i.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            i.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
        }));

        return (rows, "inventory_balances", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildWavesAsync(Guid tenantId)
    {
        var items = await _waveDb.PickingWaves
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId)
            .OrderBy(w => w.WaveNo)
            .ThenBy(w => w.Id)
            .Select(w => new { w.WaveNo, w.Status, w.CreatedAt, w.CreatedBy, w.UpdatedAt })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "waveNo", "status", "createdAt", "createdBy", "updatedAt" }
        };

        rows.AddRange(data.Select(w => new[]
        {
            w.WaveNo,
            w.Status,
            w.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            w.CreatedBy,
            w.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
        }));

        return (rows, "waves", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildPutawayProposalsAsync(Guid tenantId)
    {
        var items = await _putawayDb.PutawayProposals
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(p => new { p.WarehouseId, p.LotId, p.ItemId, p.Qty, p.CandidateLocationId, p.Score, p.Reason, p.Status, p.CreatedAt })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "warehouseId", "lotId", "itemId", "qty", "candidateLocationId", "score", "reason", "status", "createdAt" }
        };

        rows.AddRange(data.Select(p => new[]
        {
            p.WarehouseId.ToString(),
            p.LotId.ToString(),
            p.ItemId.ToString(),
            p.Qty.ToString("0.####"),
            p.CandidateLocationId.ToString(),
            p.Score.ToString(),
            p.Reason ?? "",
            p.Status,
            p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }));

        return (rows, "putaway_proposals", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildCrossDockCandidatesAsync(Guid tenantId)
    {
        var items = await _crossDockingDb.Candidates
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new { c.LotId, c.InboundOrderItemId, c.WaveItemId, c.ItemId, c.QtyAvailable, c.QtyRequested, c.QtyMatched, c.MatchScore, c.Status, c.ExpiresAt, c.CreatedAt })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "lotId", "inboundOrderItemId", "waveItemId", "itemId", "qtyAvailable", "qtyRequested", "qtyMatched", "matchScore", "status", "expiresAt", "createdAt" }
        };

        rows.AddRange(data.Select(c => new[]
        {
            c.LotId.ToString(),
            c.InboundOrderItemId.ToString(),
            c.WaveItemId.ToString(),
            c.ItemId.ToString(),
            c.QtyAvailable.ToString("0.####"),
            c.QtyRequested.ToString("0.####"),
            c.QtyMatched.ToString("0.####"),
            c.MatchScore.ToString(),
            c.Status.ToString(),
            c.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
            c.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }));

        return (rows, "cross_dock_candidates", isTruncated);
    }

    private async Task<(List<string[]> rows, string fileBase, bool isTruncated)> BuildReplenishmentTasksAsync(Guid tenantId)
    {
        var items = await _replenishmentDb.ReplenishmentTasks
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Select(r => new { r.ItemId, r.SourceLocationId, r.TargetLocationId, r.LotNo, r.RequestedQty, r.ActualQty, r.Status, r.MobileTaskId, r.CreatedAt })
            .Take(MaxRows + 1)
            .ToListAsync();

        var isTruncated = items.Count > MaxRows;
        var data = items.Take(MaxRows).ToList();

        var rows = new List<string[]>
        {
            new[] { "itemId", "sourceLocationId", "targetLocationId", "lotNo", "requestedQty", "actualQty", "status", "mobileTaskId", "createdAt" }
        };

        rows.AddRange(data.Select(r => new[]
        {
            r.ItemId.ToString(),
            r.SourceLocationId.ToString(),
            r.TargetLocationId.ToString(),
            r.LotNo,
            r.RequestedQty.ToString("0.####"),
            r.ActualQty?.ToString("0.####") ?? "",
            r.Status,
            r.MobileTaskId?.ToString() ?? "",
            r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }));

        return (rows, "replenishment_tasks", isTruncated);
    }
}
