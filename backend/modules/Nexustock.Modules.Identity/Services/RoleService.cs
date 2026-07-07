using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.Identity.Entities;

namespace Nexustock.Modules.Identity.Services;

public interface IRoleService
{
    Task<List<RoleDto>> GetAllAsync(Guid tenantId);
    Task<RoleDto?> GetByIdAsync(Guid id);
    Task<RoleDto> CreateAsync(string name, string description, Guid tenantId);
    Task<bool> UpdateAsync(Guid id, string name, string description);
    Task<bool> DeleteAsync(Guid id);
}

public class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IdentityDbContext _dbContext;

    public RoleService(RoleManager<ApplicationRole> roleManager, IdentityDbContext dbContext)
    {
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    public async Task<List<RoleDto>> GetAllAsync(Guid tenantId)
    {
        var roles = await _dbContext.Roles
            .Where(r => r.TenantId == tenantId)
            .Select(r => new RoleDto(r.Id, r.Name ?? string.Empty, r.Description, r.TenantId))
            .ToListAsync();
        return roles;
    }

    public async Task<RoleDto?> GetByIdAsync(Guid id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null) return null;
        return new RoleDto(role.Id, role.Name ?? string.Empty, role.Description, role.TenantId);
    }

    public async Task<RoleDto> CreateAsync(string name, string description, Guid tenantId)
    {
        var role = new ApplicationRole
        {
            Name = name,
            Description = description,
            TenantId = tenantId
        };
        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return new RoleDto(role.Id, role.Name!, role.Description, role.TenantId);
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string description)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null) return false;

        role.Name = name;
        role.Description = description;
        var result = await _roleManager.UpdateAsync(role);
        return result.Succeeded;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null) return false;

        var result = await _roleManager.DeleteAsync(role);
        return result.Succeeded;
    }
}

public record RoleDto(Guid Id, string Name, string Description, Guid TenantId);
