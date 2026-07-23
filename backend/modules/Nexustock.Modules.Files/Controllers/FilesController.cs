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
