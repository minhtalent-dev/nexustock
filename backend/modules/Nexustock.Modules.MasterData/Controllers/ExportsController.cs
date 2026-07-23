using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public ExportsController(MasterDataDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] string type, [FromQuery] string format = "csv")
    {
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
        var items = await _db.Products.Include(p => p.BaseUom).OrderBy(p => p.Code).Take(MaxRows + 1).ToListAsync();
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
            .Include(l => l.Zone)!.ThenInclude(z => z!.Warehouse)
            .OrderBy(l => l.Code)
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
        var items = await _db.Partners.OrderBy(p => p.Code).Take(MaxRows + 1).ToListAsync();
        var rows = new List<string[]> { new[] { "code", "name", "partnerType", "address", "taxCode" } };
        rows.AddRange(items.Select(p => new[]
        {
            p.Code, p.Name, p.PartnerType, p.Address ?? "", p.TaxCode ?? ""
        }));
        return rows;
    }
}
