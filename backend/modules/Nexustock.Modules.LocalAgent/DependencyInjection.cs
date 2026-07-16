using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.LocalAgent.Contexts;
using Nexustock.Modules.LocalAgent.Services;

namespace Nexustock.Modules.LocalAgent;

public static class DependencyInjection
{
    public static IServiceCollection AddLocalAgentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LocalAgentDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ILocalAgentService, LocalAgentService>();

        return services;
    }
}
