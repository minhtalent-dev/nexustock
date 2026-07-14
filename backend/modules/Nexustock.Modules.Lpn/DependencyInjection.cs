using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.Lpn.Services;
using Nexustock.Modules.Identity.Interceptors;

namespace Nexustock.Modules.Lpn;

public static class DependencyInjection
{
    public static IServiceCollection AddLpnModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<LpnDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(LpnDbContext).Assembly.FullName));

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }
        else
        {
            services.AddDbContext<LpnDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Lpn");

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }

        // Register Services
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ILpnService, LpnService>();

        return services;
    }
}
