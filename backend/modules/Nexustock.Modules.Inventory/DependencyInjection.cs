using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.Identity.Interceptors;

namespace Nexustock.Modules.Inventory;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<InventoryDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(InventoryDbContext).Assembly.FullName));

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }
        else
        {
            services.AddDbContext<InventoryDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Inventory");

                var auditInterceptor = sp.GetService<AuditInterceptor>();
                if (auditInterceptor != null)
                {
                    options.AddInterceptors(auditInterceptor);
                }
            });
        }

        // Register Services
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IWeightValidationService, WeightValidationService>();

        return services;
    }
}
