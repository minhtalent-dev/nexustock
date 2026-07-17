using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.LabelPrinting.Contexts;
using Nexustock.Modules.LabelPrinting.Services;

namespace Nexustock.Modules.LabelPrinting;

public static class DependencyInjection
{
    public static IServiceCollection AddLabelPrintingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddDbContext<LabelPrintingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<LabelTemplateRenderer>();
        services.AddScoped<ILabelPrintingService, LabelPrintingService>();

        return services;
    }
}
