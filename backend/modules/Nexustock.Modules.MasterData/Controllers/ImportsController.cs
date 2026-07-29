using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Controllers;

/// <summary>Controller quản lý import dữ liệu master data (CSV + xlsx).</summary>
[Authorize]
[ApiController]
[Route("api/imports")]
[Produces("application/json")]
public class ImportsController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly IUserPermissionService _permissionService;

    public ImportsController(IImportService importService, IUserPermissionService permissionService)
    {
        _importService = importService;
        _permissionService = permissionService;
    }

    private async Task<bool> CheckPermissionAsync(string permission)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permission);
    }

    [HttpGet("template")]
    public async Task<IActionResult> GetTemplate([FromQuery] string type, [FromQuery] string format = "csv")
    {
        if (!await CheckPermissionAsync("master_data.export"))
            return Forbid();

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
        if (!await CheckPermissionAsync("master_data.import"))
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Không tìm thấy file." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var username = User.Identity?.Name ?? "SYSTEM";
        try
        {
            if (ext == ".xlsx")
            {
                await using var stream = file.OpenReadStream();
                var rows = SpreadsheetReader.ReadSheetRows(stream);
                var result = await _importService.PreviewImportAsync(type, rows, username, HttpContext.RequestAborted);
                if (string.Equals(result.ErrorCsvContent, "IMPORT_TOO_LARGE", StringComparison.Ordinal))
                    return BadRequest(new { error = "IMPORT_TOO_LARGE", message = "Import exceeds 5000 data rows." });
                return Ok(result);
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var csvContent = await reader.ReadToEndAsync();
            var csvResult = await _importService.PreviewImportAsync(type, csvContent, username, HttpContext.RequestAborted);
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
        if (!await CheckPermissionAsync("master_data.import"))
            return Forbid();

        var username = User.Identity?.Name ?? "SYSTEM";
        var result = await _importService.CommitImportAsync(request.BatchId, username, HttpContext.RequestAborted);
        if (result.Success) return Ok(result);

        return result.ErrorCsvContent switch
        {
            "IMPORT_BATCH_NOT_FOUND" => NotFound(result),
            "IMPORT_BATCH_EXPIRED" or "IMPORT_BATCH_HAS_ERRORS" or "IMPORT_BATCH_ALREADY_COMMITTED" or
                "IMPORT_TARGET_MISMATCH" or "IMPORT_TARGET_STATE_INVALID" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    [HttpGet("errors/{batchId}")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportErrors(Guid batchId)
    {
        if (!await CheckPermissionAsync("master_data.import"))
            return Forbid();

        var username = User.Identity?.Name ?? "SYSTEM";
        var csv = await _importService.ExportErrorCsvAsync(batchId, username, HttpContext.RequestAborted);
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
