using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.Identity.Entities;

namespace Nexustock.Modules.Identity.Seeders;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider, 
        IEnumerable<(string Code, string Name, string Category)> permissionsList)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Seed default tenant admin role
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var adminRoleName = "Admin";
        
        var adminRole = await roleManager.FindByNameAsync(adminRoleName);
        if (adminRole == null)
        {
            adminRole = new ApplicationRole
            {
                Name = adminRoleName,
                Description = "System Administrator",
                TenantId = tenantId
            };
            var createRoleResult = await roleManager.CreateAsync(adminRole);
            if (!createRoleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create admin role: {string.Join(", ", createRoleResult.Errors.Select(e => e.Description))}");
            }
        }

        // Seed default admin user: admin@nexustock.com / AdminSecret123!
        var adminEmail = "admin@nexustock.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                TenantId = tenantId,
                IsActive = true
            };
            var result = await userManager.CreateAsync(adminUser, "AdminSecret123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, adminRoleName);
            }
            else
            {
                throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        // Seed permissions
        var existingPermissions = await dbContext.Permissions.ToListAsync();
        var permissionsToSeed = new List<Permission>();

        foreach (var p in permissionsList)
        {
            if (!existingPermissions.Any(ep => ep.Name == p.Code))
            {
                permissionsToSeed.Add(new Permission
                {
                    Name = p.Code,
                    DisplayName = p.Name,
                    Category = p.Category,
                    TenantId = tenantId
                });
            }
        }

        if (permissionsToSeed.Count > 0)
        {
            dbContext.Permissions.AddRange(permissionsToSeed);
            await dbContext.SaveChangesAsync();
        }

        // Map all tenant permissions to Admin role
        var allPermissions = await dbContext.Permissions.Where(p => p.TenantId == tenantId).ToListAsync();
        var existingRolePermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .ToListAsync();

        var newRolePermissions = new List<RolePermission>();
        foreach (var p in allPermissions)
        {
            if (!existingRolePermissions.Any(erp => erp.PermissionId == p.Id))
            {
                newRolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = p.Id,
                    TenantId = tenantId
                });
            }
        }

        if (newRolePermissions.Count > 0)
        {
            dbContext.RolePermissions.AddRange(newRolePermissions);
            await dbContext.SaveChangesAsync();
        }
    }
}
