using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Contexts;

namespace Nexustock.Modules.Identity.Services;

public interface IUserPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionName);
}

public class UserPermissionService : IUserPermissionService
{
    private readonly IdentityDbContext _dbContext;

    public UserPermissionService(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionName)
    {
        return await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_dbContext.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => new { ur, rp })
            .Join(_dbContext.Permissions,
                combined => combined.rp.PermissionId,
                p => p.Id,
                (combined, p) => p)
            .AnyAsync(p => p.Name == permissionName);
    }
}
