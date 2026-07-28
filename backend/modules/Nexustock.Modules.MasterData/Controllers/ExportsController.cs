using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Services;
using System.Text;

namespace Nexustock.Modules.MasterData.Controllers;

[Authorize]
[ApiController]
[Route("api/exports")]
public class ExportsController : ControllerBase
{
    private const int MaxRows = 5000;
    private readonly MasterDataDbContext _db;
    private readonly IUserPermissionService _permissionService;

    public ExportsController(MasterDataDbContext db, IUserPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] string type, [FromQuery] string format = "csv")
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || !await _permissionService.HasPermissionAsync(userId, "master_data.export"))
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
                "ITEMS" => (await BuildItemsAsync(), "items"),
                "LOCATIONS" => (await BuildLocationsAsync(), "locations"),
                "PARTNERS" => (await BuildPartnersAsync(), "partners"),
                "UOMS" => (await BuildUomsAsync(), "uoms"),
                "WAREHOUSES" => (await BuildWarehousesAsync(), "warehouses"),
                "ZONES" => (await BuildZonesAsync(), "zones"),
                "REASONS" => (await BuildReasonsAsync(), "reasons"),
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

    private async Task<List<string[]>> BuildItemsAsync()
    {
        var items = await _db.Products.AsNoTracking().Include(p => p.BaseUom).OrderBy(p => p.Code).ThenBy(p => p.Id).Take(MaxRows + 1).ToListAsync();
        var rows = new List<string[]> { new[] { "code", "name", "baseUomCode", "isActive" } };
        rows.AddRange(items.Select(p => new[]
        {
            p.Code, p.Name, p.BaseUom?.Code ?? "", p.IsActive ? "true" : "false"
        }));
        return rows;
    }

    private async Task<List<string[]>> BuildLocationsAsync()
    {
        var items = await _db.StorageLocations
            .AsNoTracking()
            .Include(l => l.Zone)!.ThenInclude(z => z!.Warehouse)
            .OrderBy(l => l.Code)
            .ThenBy(l => l.Id)
            .Take(MaxRows + 1)
            .ToListAsync();
        var rows = new List<string[]>
        {
            new[] { "warehouseCode", "zoneCode", "code", "xCoord", "yCoord", "zCoord", "maxCapacity" }
        };
        rows.AddRange(items.Select(l => new[]
        {
            l.Zone?.Warehouse?.Code ?? "",
            l.Zone?.Code ?? "",
            l.Code,
            l.XCoord.ToString(),
            l.YCoord.ToString(),
            l.ZCoord.ToString(),
            l.MaxCapacity.ToString("0.####")
        }));
        return rows;
    }

    private async Task<List<string[]>> BuildPartnersAsync()
    {
        var items = await _db.Partners.AsNoTracking().OrderBy(p => p.Code).ThenBy(p => p.Id).Take(MaxRows + 1).ToListAsync();
        var rows = new List<string[]> { new[] { "code", "name", "partnerType", "address", "taxCode" } };
        rows.AddRange(items.Select(p => new[]
        {
            p.Code, p.Name, p.PartnerType, p.Address ?? "", p.TaxCode ?? ""
        }));
        return rows;
    }

    private async Task<List<string[]>> BuildUomsAsync()
    {
        var items = await _db.Uoms.AsNoTracking().OrderBy(u => u.Code).ThenBy(u => u.Id).Take(MaxRows + 1).ToListAsync();
        var rows = new List<string[]> { new[] { "code", "name", "isActive" } };
        rows.AddRange(items.Select(u => new[]
        {
            u.Code, u.Name, u.IsActive ? "true" : "false"
        }));
        return rows;
    }

    private async Task<List<string[]>> BuildWarehousesAsync()
    {
        var items = await _db.Warehouses.AsNoTracking().OrderBy(w => w.Code).ThenBy(w => w.Id).Take(MaxRows + 1).ToListAsync();
        var rows = new List<string[]> { new[] { "code", "name", "description", "isActive" } };
        rows.AddRange(items.Select(w => new[]
        {
            w.Code, w.Name, w.Description ?? "", w.IsActive ? "true" : "false"
        }));
        return rows;
    }

    private async Task<List<string[]>> BuildZonesAsync()
    {
        var items = await _db.StorageZones.AsNoTracking().Include(z => z.Warehouse).OrderBy(z => z.Code).ThenBy(z => z.Id).Take(MaxRows + 1).ToListAsync();
        var rows = new List<string[]> { new[] { "warehouseCode", "code", "name", "zoneType" } };
        rows.AddRange(items.Select(z => new[]
        {
            z.Warehouse?.Code ?? "", z.Code, z.Name, z.ZoneType
        }));
        return rows;
    }

    private async Task<List<string[]>> BuildReasonsAsync()
    {
        var items = await _db.ReasonCodes.AsNoTracking().OrderBy(r => r.Code).ThenBy(r => r.Id).Take(MaxRows + 1).ToListAsync();
        var rows = new List<string[]> { new[] { "code", "reasonType", "description", "isActive" } };
        rows.AddRange(items.Select(r => new[]
        {
            r.Code, r.ReasonType, r.Description, r.IsActive ? "true" : "false"
        }));
        return rows;
    }
}
