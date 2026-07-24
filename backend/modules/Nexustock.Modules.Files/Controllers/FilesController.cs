using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Files.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private static readonly HashSet<string> AllowedInlineMime = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "application/pdf"
    };

    private readonly IFileStorageService _storage;
    private readonly IAttachmentService _attachments;
    private readonly IUserPermissionService _permissions;

    public FilesController(
        IFileStorageService storage,
        IAttachmentService attachments,
        IUserPermissionService permissions)
    {
        _storage = storage;
        _attachments = attachments;
        _permissions = permissions;
    }

    private async Task<bool> HasAsync(string permission)
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var id)) return false;
        return await _permissions.HasPermissionAsync(id, permission);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (!await HasAsync("files.upload")) return Forbid();
        try
        {
            var result = await _storage.UploadAsync(file, User.Identity?.Name, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpPost("attachments")]
    public async Task<IActionResult> Bind([FromBody] BindAttachmentRequest request)
    {
        if (!await HasAsync("files.upload")) return Forbid();
        try
        {
            var result = await _attachments.BindAsync(request, User.Identity?.Name, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpGet("attachments")]
    public async Task<IActionResult> List([FromQuery] string entityType, [FromQuery] Guid entityId)
    {
        if (!await HasAsync("files.read")) return Forbid();
        var items = await _attachments.ListAsync(entityType, entityId, HttpContext.RequestAborted);
        return Ok(new { items });
    }

    [HttpGet("attachments/{id:guid}/content")]
    public async Task<IActionResult> GetContent(Guid id, [FromQuery] string disposition = "inline")
    {
        if (!await HasAsync("files.read")) return Forbid();

        var mode = (disposition ?? "inline").Trim().ToLowerInvariant();
        if (mode is not ("inline" or "attachment"))
        {
            return BadRequest(new { error = "ATTACHMENT_DISPOSITION_INVALID", message = "Disposition must be inline or attachment" });
        }

        try
        {
            var content = await _attachments.OpenContentAsync(id, mode, HttpContext.RequestAborted);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Cache-Control"] = "private, no-store"; // Chặn lưu cache file nhạy cảm

            // If non-previewable file requested as inline, force attachment mode
            if (mode == "inline" && !AllowedInlineMime.Contains(content.ContentType))
            {
                mode = "attachment";
            }

            // Sanitize filename triệt để chống Header Injection và path traversal
            var safeName = content.FileName
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("/", "_")
                .Replace("\\", "_");

            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "attachment";
            }

            var result = new FileStreamResult(content.Stream, content.ContentType)
            {
                EnableRangeProcessing = content.Stream.CanSeek
            };

            // Set Content-Disposition tường minh
            var contentDisposition = new System.Net.Mime.ContentDisposition
            {
                FileName = safeName,
                Inline = (mode == "inline")
            };
            Response.Headers["Content-Disposition"] = contentDisposition.ToString();

            return result;
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await HasAsync("files.delete")) return Forbid();
        try
        {
            await _attachments.DeleteAsync(id, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }
}
