using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Wave.Contexts;
using Nexustock.Modules.Wave.Services;

namespace Nexustock.Modules.Wave;

public static class DependencyInjection
{
    public static IServiceCollection AddWaveModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WaveDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));
        
        services.AddScoped<IWaveService, WaveService>();
        
        return services;
    }
}
