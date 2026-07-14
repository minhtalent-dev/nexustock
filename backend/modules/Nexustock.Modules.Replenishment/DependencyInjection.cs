using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Replenishment.Contexts;
using Nexustock.Modules.Replenishment.Services;
using Nexustock.Modules.Identity.Interceptors;

namespace Nexustock.Modules.Replenishment;

public static class DependencyInjection
{
    public static IServiceCollection AddReplenishmentModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<ReplenishmentDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(ReplenishmentDbContext).Assembly.FullName));

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }
        else
        {
            services.AddDbContext<ReplenishmentDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Replenishment");

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }

        // Register Services
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<IReplenishmentService, ReplenishmentService>();

        return services;
    }
}
