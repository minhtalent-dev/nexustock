using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Serial.Contexts;
using Nexustock.Modules.Serial.Services;

namespace Nexustock.Modules.Serial;

public static class DependencyInjection
{
    public static IServiceCollection AddSerialModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SerialDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ISerialService, SerialService>();

        return services;
    }
}
