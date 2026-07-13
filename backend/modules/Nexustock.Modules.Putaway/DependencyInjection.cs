using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Putaway.Contexts;
using Nexustock.Modules.Putaway.Services;
using Nexustock.Modules.Identity.Interceptors;

namespace Nexustock.Modules.Putaway;

public static class DependencyInjection
{
    public static IServiceCollection AddPutawayModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<PutawayDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(PutawayDbContext).Assembly.FullName));

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }
        else
        {
            services.AddDbContext<PutawayDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Putaway");

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }

        // Register Services
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<IPutawayService, PutawayService>();

        return services;
    }
}
