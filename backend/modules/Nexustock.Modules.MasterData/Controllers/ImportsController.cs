using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Controllers;

/// <summary>
/// Controller quản lý import dữ liệu - dùng chung cho tất cả loại master data.
/// </summary>
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

    /// <summary>
    /// Tải template CSV để nhập liệu cho loại dữ liệu chỉ định.
    /// </summary>
    /// <param name="type">Loại master data: ITEMS, LOCATIONS, PARTNERS</param>
    [HttpGet("template")]
    public IActionResult GetTemplate([FromQuery] string type)
    {
        try
        {
            var csv = _importService.GetTemplateCsv(type);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"template_{type.ToLowerInvariant()}.csv");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Preview import: phân tích file CSV, validate dữ liệu nhưng không ghi vào DB.
    /// </summary>
    /// <param name="type">Loại master data: ITEMS, LOCATIONS, PARTNERS</param>
    /// <param name="file">File CSV cần preview</param>
    [HttpPost("preview")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> Preview([FromQuery] string type, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Không tìm thấy file." });

        using var reader = new StreamReader(file.OpenReadStream());
        var csvContent = await reader.ReadToEndAsync();

        var result = await _importService.PreviewImportAsync(type, csvContent, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Commit import: xác nhận import batch đã preview.
    /// </summary>
    [HttpPost("commit")]
    public async Task<IActionResult> Commit([FromBody] CommitImportRequest request)
    {
        var result = await _importService.CommitImportAsync(request.BatchId, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Xuất file CSV các dòng lỗi của batch import.
    /// </summary>
    [HttpGet("errors/{batchId}")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportErrors(Guid batchId)
    {
        var csv = await _importService.ExportErrorCsvAsync(batchId, HttpContext.RequestAborted);
        if (csv == null)
            return NotFound(new { error = "Không tìm thấy batch hoặc batch không có lỗi." });

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"errors_{batchId}.csv");
    }
}

/// <summary>
/// Request body cho commit import.
/// </summary>
public class CommitImportRequest
{
    public Guid BatchId { get; set; }
}
