using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.CrossDocking.Contexts;
using Nexustock.Modules.CrossDocking.Services;

namespace Nexustock.Modules.CrossDocking;

public static class DependencyInjection
{
    public static IServiceCollection AddCrossDockingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CrossDockingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ICrossDockingService, CrossDockingService>();

        return services;
    }
}
