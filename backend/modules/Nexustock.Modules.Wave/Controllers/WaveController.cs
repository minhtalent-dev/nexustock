using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Wave.DTOs;
using Nexustock.Modules.Wave.Services;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inventory.Services;

namespace Nexustock.Modules.Wave.Controllers;

[Authorize]
[ApiController]
[Route("api/waves")]
public class WaveController : ControllerBase
{
    private readonly IWaveService _waveService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;

    public WaveController(
        IWaveService waveService,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService)
    {
        _waveService = waveService;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpGet]
    public async Task<IActionResult> GetWaves()
    {
        if (!await HasPermissionAsync("Wave.Manage")) return Forbid();
        var waves = await _waveService.GetWavesAsync(GetTenantId());
        return Ok(waves);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWaveDetails(Guid id)
    {
        if (!await HasPermissionAsync("Wave.Manage")) return Forbid();
        try
        {
            var wave = await _waveService.GetWaveDetailsAsync(GetTenantId(), id);
            return Ok(wave);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateWave([FromBody] CreateWaveDto dto)
    {
        if (!await HasPermissionAsync("Wave.Manage")) return Forbid();
        var username = User.Identity?.Name ?? "System";
        try
        {
            var waveId = await _waveService.CreateWaveAsync(GetTenantId(), username, dto);
            return Ok(new { id = waveId, message = "Tạo đợt Wave thành công." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/release")]
    public async Task<IActionResult> ReleaseWave(Guid id)
    {
        if (!await HasPermissionAsync("Wave.Manage")) return Forbid();
        var username = User.Identity?.Name ?? "System";
        try
        {
            await _waveService.ReleaseWaveAsync(GetTenantId(), username, id);
            return Ok(new { message = "Release Wave thành công." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("pick-tasks/complete")]
    public async Task<IActionResult> CompletePickTask([FromBody] CompleteWavePickDto dto)
    {
        // Permission cho việc Pick hàng di động
        if (!await HasPermissionAsync("rf_mobile_core_scan.update") && !await HasPermissionAsync("Wave.Manage")) 
            return Forbid();
            
        var username = User.Identity?.Name ?? "System";
        try
        {
            await _waveService.CompletePickTaskAsync(GetTenantId(), username, dto);
            return Ok(new { message = "Xác nhận lấy hàng thành công." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/sort")]
    public async Task<IActionResult> SortItem(Guid id, [FromBody] SortRequestDto dto)
    {
        if (!await HasPermissionAsync("Wave.Manage")) return Forbid();
        try
        {
            var res = await _waveService.SortItemAsync(GetTenantId(), id, dto);
            return Ok(res);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteWave(Guid id)
    {
        if (!await HasPermissionAsync("Wave.Manage")) return Forbid();
        var username = User.Identity?.Name ?? "System";
        try
        {
            await _waveService.CompleteWaveAsync(GetTenantId(), username, id);
            return Ok(new { message = "Hoàn thành phân chia đợt Wave." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
