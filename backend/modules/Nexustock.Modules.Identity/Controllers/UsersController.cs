using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Identity.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
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
        var users = await _userService.GetAllAsync(tenantId);
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null || user.TenantId != GetTenantId()) return NotFound();
        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var tenantId = GetTenantId();
        var (succeeded, errors) = await _userService.CreateAsync(
            request.Email, request.Password, request.FullName, tenantId, request.Roles ?? Array.Empty<string>());

        if (!succeeded) return BadRequest(new { errors });
        return Ok(new { message = "User created successfully" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null || user.TenantId != GetTenantId()) return NotFound();

        var result = await _userService.UpdateAsync(id, request.FullName, request.IsActive);
        if (!result) return BadRequest(new { message = "Update failed" });

        return Ok(new { message = "User updated successfully" });
    }

    [HttpPost("{id}/roles")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null || user.TenantId != GetTenantId()) return NotFound();

        var result = await _userService.AssignRolesAsync(id, request.Roles);
        if (!result) return BadRequest(new { message = "Role assignment failed" });

        return Ok(new { message = "Roles assigned successfully" });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet("/api/me/permissions")]
    public async Task<IActionResult> GetMyPermissions()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var permissions = await _userService.GetUserPermissionsAsync(userId);
        return Ok(permissions);
    }

    [HttpGet("/api/me/roles")]
    public async Task<IActionResult> GetMyRoles()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return Unauthorized();

        return Ok(user.Roles);
    }

    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null || user.TenantId != GetTenantId()) return NotFound();

        var permissions = await _userService.GetUserPermissionsAsync(id);
        return Ok(permissions);
    }
}

public record CreateUserRequest(string Email, string Password, string FullName, string[]? Roles);
public record UpdateUserRequest(string FullName, bool IsActive);
public record AssignRolesRequest(string[] Roles);
