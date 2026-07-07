using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Identity.Controllers;

[Authorize]
[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IPermissionService _permissionService;

    public RolesController(IRoleService roleService, IPermissionService permissionService)
    {
        _roleService = roleService;
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
        var roles = await _roleService.GetAllAsync(tenantId);
        return Ok(roles);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role == null || role.TenantId != GetTenantId()) return NotFound();
        return Ok(role);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var tenantId = GetTenantId();
        var role = await _roleService.CreateAsync(request.Name, request.Description, tenantId);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role == null || role.TenantId != GetTenantId()) return NotFound();

        var result = await _roleService.UpdateAsync(id, request.Name, request.Description);
        if (!result) return BadRequest(new { message = "Update failed" });

        return Ok(new { message = "Role updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role == null || role.TenantId != GetTenantId()) return NotFound();

        var result = await _roleService.DeleteAsync(id);
        if (!result) return BadRequest(new { message = "Delete failed" });

        return Ok(new { message = "Role deleted successfully" });
    }

    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(Guid id)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role == null || role.TenantId != GetTenantId()) return NotFound();

        var permissions = await _permissionService.GetByRoleAsync(id);
        return Ok(permissions);
    }

    [HttpPost("{id}/permissions")]
    public async Task<IActionResult> SetPermissions(Guid id, [FromBody] SetRolePermissionsRequest request)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role == null || role.TenantId != GetTenantId()) return NotFound();

        await _permissionService.SetRolePermissionsAsync(id, request.PermissionIds, GetTenantId());
        return Ok(new { message = "Role permissions updated successfully" });
    }
}

public record CreateRoleRequest(string Name, string Description);
public record UpdateRoleRequest(string Name, string Description);
public record SetRolePermissionsRequest(Guid[] PermissionIds);
