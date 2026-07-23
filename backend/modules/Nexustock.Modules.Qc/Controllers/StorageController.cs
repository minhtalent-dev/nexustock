using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Qc.Dtos;

namespace Nexustock.Modules.Qc.Controllers;

/// <summary>Compat QC upload — delegate sang Files module.</summary>
[Authorize]
[ApiController]
[Route("api/storage")]
public class StorageController : ControllerBase
{
    private readonly IFileStorageService _fileStorage;

    public StorageController(IFileStorageService fileStorage)
    {
        _fileStorage = fileStorage;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        try
        {
            var result = await _fileStorage.UploadAsync(file, User.Identity?.Name, HttpContext.RequestAborted);
            return Ok(new UploadResponseDto { Url = result.Url });
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }
}
