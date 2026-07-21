using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Readiness.Contexts;
using Nexustock.Modules.Readiness.Services;

namespace Nexustock.Modules.Readiness;

public static class DependencyInjection
{
    public static IServiceCollection AddReadinessModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<ReadinessDbContext>(options =>
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(ReadinessDbContext).Assembly.FullName)));

        services.AddScoped<IReadinessProbeService, ReadinessProbeService>();
        services.AddScoped<IReadinessService, ReadinessService>();
        services.AddScoped<ICutoverFreezeService, CutoverFreezeService>();

        return services;
    }
}
