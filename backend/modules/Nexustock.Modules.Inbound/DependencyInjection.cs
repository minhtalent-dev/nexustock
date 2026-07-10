using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Services;
using Nexustock.Modules.Identity.Interceptors;

namespace Nexustock.Modules.Inbound;

public static class DependencyInjection
{
    public static IServiceCollection AddInboundModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<InboundDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(InboundDbContext).Assembly.FullName));
                
                // Add AuditInterceptor from Identity module to track audit logs
                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }
        else
        {
            services.AddDbContext<InboundDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Inbound");
                
                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }

        // Register Inbound Services
        services.AddScoped<ITenantProvider, TenantProvider>();

        return services;
    }
}
