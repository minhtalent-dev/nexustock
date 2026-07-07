using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.Identity.Entities;

namespace Nexustock.Modules.Identity.Services;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(Guid tenantId);
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<(bool Succeeded, string[] Errors)> CreateAsync(string email, string password, string fullName, Guid tenantId, string[] roles);
    Task<bool> UpdateAsync(Guid id, string fullName, bool isActive);
    Task<bool> AssignRolesAsync(Guid id, string[] roles);
    Task<List<string>> GetUserPermissionsAsync(Guid id);
}

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IdentityDbContext _dbContext;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IdentityDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    public async Task<List<UserDto>> GetAllAsync(Guid tenantId)
    {
        var users = await _userManager.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();

        var dtos = new List<UserDto>();
        foreach (var user in users)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            dtos.Add(new UserDto(user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive, user.TenantId, userRoles.ToArray()));
        }

        return dtos;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return null;

        var userRoles = await _userManager.GetRolesAsync(user);
        return new UserDto(user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive, user.TenantId, userRoles.ToArray());
    }

    public async Task<(bool Succeeded, string[] Errors)> CreateAsync(string email, string password, string fullName, Guid tenantId, string[] roles)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            TenantId = tenantId,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        if (roles != null && roles.Length > 0)
        {
            var roleResult = await _userManager.AddToRolesAsync(user, roles);
            if (!roleResult.Succeeded)
                return (false, roleResult.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<bool> UpdateAsync(Guid id, string fullName, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return false;

        user.FullName = fullName;
        user.IsActive = isActive;

        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<bool> AssignRolesAsync(Guid id, string[] roles)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return false;

        var currentRoles = await _userManager.GetRolesAsync(user);
        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded) return false;

        var addResult = await _userManager.AddToRolesAsync(user, roles);
        return addResult.Succeeded;
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return new List<string>();

        var userRoles = await _userManager.GetRolesAsync(user);
        
        // Lấy danh sách RoleIds
        var roleIds = await _dbContext.Roles
            .Where(r => r.TenantId == user.TenantId && userRoles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        if (roleIds.Count == 0) return new List<string>();

        // Lấy danh sách permissions từ RolePermissions join Permissions
        var permissions = await _dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync();

        return permissions;
    }
}

public record UserDto(Guid Id, string Email, string FullName, bool IsActive, Guid TenantId, string[] Roles);
