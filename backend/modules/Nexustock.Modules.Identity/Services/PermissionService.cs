using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.Identity.Entities;

namespace Nexustock.Modules.Identity.Services;

public interface IPermissionService
{
    Task<List<Permission>> GetAllAsync(Guid tenantId);
    Task<Permission?> GetByIdAsync(Guid id);
    Task<Permission> CreateAsync(string name, string displayName, string category, Guid tenantId);
    Task<bool> UpdateAsync(Guid id, string name, string displayName, string category);
    Task<bool> DeleteAsync(Guid id);
    Task<List<Permission>> GetByRoleAsync(Guid roleId);
    Task SetRolePermissionsAsync(Guid roleId, Guid[] permissionIds, Guid tenantId);
}

public class PermissionService : IPermissionService
{
    private readonly IdentityDbContext _dbContext;

    public PermissionService(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Permission>> GetAllAsync(Guid tenantId)
    {
        return await _dbContext.Permissions
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.DisplayName)
            .ToListAsync();
    }

    public async Task<Permission?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Permissions.FindAsync(id);
    }

    public async Task<Permission> CreateAsync(string name, string displayName, string category, Guid tenantId)
    {
        var permission = new Permission
        {
            Name = name,
            DisplayName = displayName,
            Category = category,
            TenantId = tenantId
        };

        _dbContext.Permissions.Add(permission);
        await _dbContext.SaveChangesAsync();

        return permission;
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string displayName, string category)
    {
        var permission = await _dbContext.Permissions.FindAsync(id);
        if (permission == null) return false;

        permission.Name = name;
        permission.DisplayName = displayName;
        permission.Category = category;

        _dbContext.Permissions.Update(permission);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var permission = await _dbContext.Permissions.FindAsync(id);
        if (permission == null) return false;

        _dbContext.Permissions.Remove(permission);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<Permission>> GetByRoleAsync(Guid roleId)
    {
        var permissionIds = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        return await _dbContext.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .ToListAsync();
    }

    public async Task SetRolePermissionsAsync(Guid roleId, Guid[] permissionIds, Guid tenantId)
    {
        // Xoá các permission cũ
        var existing = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();
        _dbContext.RolePermissions.RemoveRange(existing);

        // Thêm permission mới
        foreach (var permissionId in permissionIds)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
                TenantId = tenantId
            });
        }

        await _dbContext.SaveChangesAsync();
    }
}
