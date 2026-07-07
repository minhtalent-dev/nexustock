using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData;

public static class DependencyInjection
{
    public static IServiceCollection AddMasterDataModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<MasterDataDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(MasterDataDbContext).Assembly.FullName));
                options.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());
            });
        }
        else
        {
            // Test environment: InMemory DB được đăng ký bởi CustomWebApplicationFactory
            services.AddDbContext<MasterDataDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("NexustockTest_Fallback");
                options.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());
            });
        }

        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ILookupMasterDataService, LookupMasterDataService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IImportService, ImportService>();

        return services;
    }
}
