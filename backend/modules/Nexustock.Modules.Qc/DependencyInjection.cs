using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Qc.Contexts;
using Nexustock.Modules.Qc.Services;
using Nexustock.Modules.Qc.Abstractions;
using Nexustock.Modules.Identity.Interceptors;

namespace Nexustock.Modules.Qc;

public static class DependencyInjection
{
    public static IServiceCollection AddQcModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<QcDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(QcDbContext).Assembly.FullName));
                
                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }
        else
        {
            services.AddDbContext<QcDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Qc");
                
                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }

        // Register QC Services
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<IQcGateService, QcGateService>();
        services.AddScoped<IQcAttachmentReadService, QcAttachmentReadService>();
        services.AddScoped<Files.Services.IAttachmentLifecycleObserver, QcAttachmentCompatibilityObserver>();

        return services;
    }
}

