using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Rma.Contexts;
using Nexustock.Modules.Rma.Services;

namespace Nexustock.Modules.Rma;

public static class DependencyInjection
{
    public static IServiceCollection AddRmaModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RmaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));
        services.AddScoped<IRmaService, RmaService>();
        return services;
    }
}
