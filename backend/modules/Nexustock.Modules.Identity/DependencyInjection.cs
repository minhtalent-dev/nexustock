using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.Identity.Entities;
using Nexustock.Modules.Identity.Interceptors;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Identity;

/// <summary>
/// Registers Identity module services, EF Core DbContext, and ASP.NET Core Identity.
/// Must be called from the Host's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<IdentityDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName));
                options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
            });
        }
        else
        {
            services.AddDbContext<IdentityDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Identity");
                options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
            });
        }

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Password policy production-ready
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<IdentityDbContext>();

        // Add IHttpContextAccessor required for tenant resolution
        services.AddHttpContextAccessor();

        // Register Identity Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserPermissionService, UserPermissionService>();

        // Register Audit SaveChanges Interceptor (global cho IdentityDb và MasterDataDb)
        services.AddSingleton<AuditInterceptor>();
        services.AddSingleton<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>(sp => sp.GetRequiredService<AuditInterceptor>());

        return services;
    }
}
