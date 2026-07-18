using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.ErpIntegration.Contexts;
using Nexustock.Modules.ErpIntegration.Services;

namespace Nexustock.Modules.ErpIntegration;

public static class DependencyInjection
{
    public static IServiceCollection AddErpIntegrationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddDbContext<ErpIntegrationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<IPayloadHashService, PayloadHashService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IContractVersionService, ContractVersionService>();
        services.AddScoped<IIntegrationMappingService, IntegrationMappingService>();
        services.AddScoped<IImportPreviewService, ImportPreviewService>();
        services.AddScoped<IImportCommitService, ImportCommitService>();

        return services;
    }
}
