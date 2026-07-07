using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Identity.Controllers;

[Authorize]
[ApiController]
[Route("api/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    private Guid GetTenantId()
    {
        var tenantIdClaim = User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = GetTenantId();
        var permissions = await _permissionService.GetAllAsync(tenantId);
        return Ok(permissions);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var permission = await _permissionService.GetByIdAsync(id);
        if (permission == null) return NotFound();
        return Ok(permission);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePermissionRequest request)
    {
        var tenantId = GetTenantId();
        var permission = await _permissionService.CreateAsync(
            request.Name, request.DisplayName, request.Category, tenantId);
        return CreatedAtAction(nameof(GetById), new { id = permission.Id }, permission);
    }
}

public record CreatePermissionRequest(string Name, string DisplayName, string Category);
