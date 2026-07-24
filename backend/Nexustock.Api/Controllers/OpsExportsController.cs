using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Rma.Contexts;
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
    private readonly IUserPermissionService _permissionService;

    public OpsExportsController(
        InboundDbContext inboundDb,
        InventoryDbContext inventoryDb,
        RmaDbContext rmaDb,
        IUserPermissionService permissionService)
    {
        _inboundDb = inboundDb;
        _inventoryDb = inventoryDb;
        _rmaDb = rmaDb;
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

        var t = (type ?? "").Trim().ToUpperInvariant();
        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        if (fmt is not ("csv" or "xlsx"))
            return BadRequest(new { error = "EXPORT_TYPE_INVALID", message = "format must be csv or xlsx" });

        List<string[]> rows;
        string fileBase;
        try
        {
            (rows, fileBase) = t switch
            {
                "INBOUND_ORDERS" => (await BuildInboundOrdersAsync(), "inbound_orders"),
                "SHIPMENTS" => (await BuildShipmentsAsync(), "shipments"),
                "STOCKTAKES" => (await BuildStocktakesAsync(), "stocktakes"),
                "RMA" => (await BuildRmaAsync(), "rma_requests"),
                _ => throw new ArgumentException("EXPORT_TYPE_INVALID")
            };
        }
        catch (ArgumentException)
        {
            return BadRequest(new { error = "EXPORT_TYPE_INVALID" });
        }

        if (rows.Count - 1 > MaxRows)
            return BadRequest(new { error = "EXPORT_TOO_LARGE", message = "Export exceeds 5000 rows." });

        if (fmt == "xlsx")
        {
            var bytes = SpreadsheetReader.WriteXlsx(rows);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileBase}.xlsx");
        }

        var csv = SpreadsheetReader.RowsToCsv(rows);
        var bom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bom, "text/csv", $"{fileBase}.csv");
    }

    private async Task<List<string[]>> BuildInboundOrdersAsync()
    {
        var items = await _inboundDb.InboundOrders
            .OrderBy(o => o.OrderNo)
            .Take(MaxRows + 1)
            .ToListAsync();

        var rows = new List<string[]>
        {
            new[] { "orderNo", "status", "partnerId", "createdAt", "createdBy" }
        };

        rows.AddRange(items.Select(o => new[]
        {
            o.OrderNo,
            o.Status.ToString(),
            o.PartnerId.ToString(),
            o.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            o.CreatedBy ?? ""
        }));

        return rows;
    }

    private async Task<List<string[]>> BuildShipmentsAsync()
    {
        var items = await _inventoryDb.Shipments
            .OrderBy(s => s.ShipmentNo)
            .Take(MaxRows + 1)
            .ToListAsync();

        var rows = new List<string[]>
        {
            new[] { "shipmentNo", "status", "partnerId", "createdAt", "createdBy" }
        };

        rows.AddRange(items.Select(s => new[]
        {
            s.ShipmentNo,
            s.Status,
            s.PartnerId.ToString(),
            s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            s.CreatedBy
        }));

        return rows;
    }

    private async Task<List<string[]>> BuildStocktakesAsync()
    {
        var items = await _inventoryDb.Stocktakes
            .OrderBy(s => s.StocktakeNo)
            .Take(MaxRows + 1)
            .ToListAsync();

        var rows = new List<string[]>
        {
            new[] { "stocktakeNo", "status", "totalVarianceAmount", "createdAt", "createdBy" }
        };

        rows.AddRange(items.Select(s => new[]
        {
            s.StocktakeNo,
            s.Status,
            s.TotalVarianceAmount.ToString("0.####"),
            s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            s.CreatedBy
        }));

        return rows;
    }

    private async Task<List<string[]>> BuildRmaAsync()
    {
        var items = await _rmaDb.RmaRequests
            .OrderBy(r => r.RmaNo)
            .Take(MaxRows + 1)
            .ToListAsync();

        var rows = new List<string[]>
        {
            new[] { "rmaNo", "status", "customerId", "referenceNo", "createdAt", "createdBy" }
        };

        rows.AddRange(items.Select(r => new[]
        {
            r.RmaNo,
            r.Status,
            r.CustomerId.ToString(),
            r.ReferenceNo ?? "",
            r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            r.CreatedBy
        }));

        return rows;
    }
}
