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
            services.AddDbContext<MasterDataDbContext>(options =>
                options.UseNpgsql(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(MasterDataDbContext).Assembly.FullName)));
        }
        else
        {
            // Test environment: InMemory DB được đăng ký bởi CustomWebApplicationFactory
            services.AddDbContext<MasterDataDbContext>(options =>
                options.UseInMemoryDatabase("NexustockTest_Fallback"));
        }

        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ILookupMasterDataService, LookupMasterDataService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IImportService, ImportService>();

        return services;
    }
}
