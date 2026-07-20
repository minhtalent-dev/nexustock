using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.LaborTracking.Contexts;
using Nexustock.Modules.LaborTracking.Services;
using Nexustock.Modules.LaborTracking.Jobs;

namespace Nexustock.Modules.LaborTracking;

public static class DependencyInjection
{
    public static IServiceCollection AddLaborTrackingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LaborTrackingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ILaborTrackingService, LaborTrackingService>();
        services.AddHostedService<LaborSessionTimeoutWorker>();

        return services;
    }
}

