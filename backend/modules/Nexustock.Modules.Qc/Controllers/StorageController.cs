using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Nexustock.Modules.Qc.Dtos;

namespace Nexustock.Modules.Qc.Controllers;

[Authorize]
[ApiController]
[Route("api/storage")]
public class StorageController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public StorageController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded or file is empty");
        }

        var uploadPath = _configuration["UploadSettings:UploadPath"] ?? "D:\\NexustockUploads";
        var requestPath = _configuration["UploadSettings:RequestPath"] ?? "/uploads";

        if (!Directory.Exists(uploadPath))
        {
            Directory.CreateDirectory(uploadPath);
        }

        var ext = Path.GetExtension(file.FileName);
        var newFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadPath, newFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl = $"{requestPath}/{newFileName}";
        return Ok(new UploadResponseDto { Url = relativeUrl });
    }
}
