using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Exceptions.Services;
using Nexustock.Modules.Identity.Interceptors;

namespace Nexustock.Modules.Exceptions;

public static class DependencyInjection
{
    public static IServiceCollection AddExceptionsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<ExceptionsDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(ExceptionsDbContext).Assembly.FullName));

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }
        else
        {
            services.AddDbContext<ExceptionsDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Exceptions");

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }

        // Register Services
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddHttpClient();
        services.AddHostedService<Nexustock.Modules.Exceptions.Jobs.SlaMonitorJob>();

        return services;
    }
}
