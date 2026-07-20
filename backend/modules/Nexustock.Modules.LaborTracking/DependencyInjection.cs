using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.LaborTracking.Contexts;
using Nexustock.Modules.LaborTracking.Services;

namespace Nexustock.Modules.LaborTracking;

public static class DependencyInjection
{
    public static IServiceCollection AddLaborTrackingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LaborTrackingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ILaborTrackingService, LaborTrackingService>();

        return services;
    }
}
