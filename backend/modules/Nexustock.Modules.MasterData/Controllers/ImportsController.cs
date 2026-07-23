using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Controllers;

/// <summary>Controller quản lý import dữ liệu master data (CSV + xlsx).</summary>
[ApiController]
[Route("api/imports")]
[Produces("application/json")]
public class ImportsController : ControllerBase
{
    private readonly IImportService _importService;

    public ImportsController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpGet("template")]
    public IActionResult GetTemplate([FromQuery] string type, [FromQuery] string format = "csv")
    {
        try
        {
            var csv = _importService.GetTemplateCsv(type);
            var fmt = (format ?? "csv").Trim().ToLowerInvariant();
            if (fmt == "xlsx")
            {
                var rows = CsvParser.Parse(csv);
                var bytes = SpreadsheetReader.WriteXlsx(rows);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"template_{type.ToLowerInvariant()}.xlsx");
            }

            var bom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bom, "text/csv", $"template_{type.ToLowerInvariant()}.csv");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("preview")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Preview([FromQuery] string type, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Không tìm thấy file." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        try
        {
            if (ext == ".xlsx")
            {
                await using var stream = file.OpenReadStream();
                var rows = SpreadsheetReader.ReadSheetRows(stream);
                var result = await _importService.PreviewImportAsync(type, rows, HttpContext.RequestAborted);
                if (string.Equals(result.ErrorCsvContent, "IMPORT_TOO_LARGE", StringComparison.Ordinal))
                    return BadRequest(new { error = "IMPORT_TOO_LARGE", message = "Import exceeds 5000 data rows." });
                return Ok(result);
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var csvContent = await reader.ReadToEndAsync();
            var csvResult = await _importService.PreviewImportAsync(type, csvContent, HttpContext.RequestAborted);
            if (string.Equals(csvResult.ErrorCsvContent, "IMPORT_TOO_LARGE", StringComparison.Ordinal))
                return BadRequest(new { error = "IMPORT_TOO_LARGE", message = "Import exceeds 5000 data rows." });
            return Ok(csvResult);
        }
        catch (InvalidOperationException ex) when (ex.Message == "IMPORT_PARSE_FAILED")
        {
            return BadRequest(new { error = "IMPORT_PARSE_FAILED", message = "Unable to parse spreadsheet." });
        }
    }

    [HttpPost("commit")]
    public async Task<IActionResult> Commit([FromBody] CommitImportRequest request)
    {
        var result = await _importService.CommitImportAsync(request.BatchId, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("errors/{batchId}")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportErrors(Guid batchId)
    {
        var csv = await _importService.ExportErrorCsvAsync(batchId, HttpContext.RequestAborted);
        if (csv == null)
            return NotFound(new { error = "Không tìm thấy batch hoặc batch không có lỗi." });

        var bom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bom, "text/csv", $"errors_{batchId}.csv");
    }
}

public class CommitImportRequest
{
    public Guid BatchId { get; set; }
}
