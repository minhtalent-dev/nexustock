using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Files.Controllers;

[Authorize]
[ApiController]
[Route("api/files/storage-settings")]
public class FileStorageSettingsController : ControllerBase
{
    private readonly IFileStorageSettingsService _settings;
    private readonly IUserPermissionService _permissions;

    public FileStorageSettingsController(IFileStorageSettingsService settings, IUserPermissionService permissions)
    {
        _settings = settings;
        _permissions = permissions;
    }

    private async Task<bool> HasManageAsync()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var id)) return false;
        return await _permissions.HasPermissionAsync(id, "files.storage.manage");
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!await HasManageAsync()) return Forbid();
        return Ok(await _settings.GetAsync(HttpContext.RequestAborted));
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] UpsertStorageSettingsRequest request)
    {
        if (!await HasManageAsync()) return Forbid();
        try
        {
            return Ok(await _settings.UpsertAsync(request, User.Identity?.Name, HttpContext.RequestAborted));
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] UpsertStorageSettingsRequest? draft)
    {
        if (!await HasManageAsync()) return Forbid();
        try
        {
            return Ok(await _settings.TestAsync(draft, HttpContext.RequestAborted));
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }
}
