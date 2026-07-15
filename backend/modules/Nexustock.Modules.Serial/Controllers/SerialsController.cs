using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using Nexustock.Modules.Serial.Services;
using Nexustock.Modules.Serial.DTOs;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Serial.Controllers;

[ApiController]
[Route("api/serials")]
[Authorize]
public class SerialsController : ControllerBase
{
    private readonly ISerialService _serialService;
    private readonly IUserPermissionService _permissionService;

    public SerialsController(ISerialService serialService, IUserPermissionService permissionService)
    {
        _serialService = serialService;
        _permissionService = permissionService;
    }

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpPost("receive")]
    public async Task<IActionResult> Receive([FromBody] ReceiveSerialDto dto)
    {
        if (!await HasPermissionAsync("serial.create"))
        {
            return Forbid();
        }

        var result = await _serialService.ReceiveSerialAsync(dto, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateSerialDto dto)
    {
        if (!await HasPermissionAsync("serial.execute"))
        {
            return Forbid();
        }

        var valid = await _serialService.ValidateSerialForPickAsync(dto);
        return Ok(new { valid });
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file, [FromQuery] Guid itemId, [FromQuery] Guid locationId)
    {
        if (!await HasPermissionAsync("serial.create"))
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
            return BadRequest("File không hợp lệ hoặc rỗng.");

        using var stream = file.OpenReadStream();
        var result = await _serialService.ImportFromCsvAsync(stream, itemId, locationId, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpGet("{serialNo}")]
    public async Task<IActionResult> GetBySerialNo(string serialNo)
    {
        if (!await HasPermissionAsync("serial.read"))
        {
            return Forbid();
        }

        var timeline = await _serialService.GetSerialTimelineAsync(serialNo);
        return Ok(timeline);
    }
}
