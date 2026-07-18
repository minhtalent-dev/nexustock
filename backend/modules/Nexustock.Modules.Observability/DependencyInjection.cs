using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Observability.Contexts;

using Nexustock.Modules.Observability.Services;

namespace Nexustock.Modules.Observability;

public static class DependencyInjection
{
    public static IServiceCollection AddObservabilityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ObservabilityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddHttpContextAccessor();
        services.AddScoped<ITraceContext, TraceContext>();
        services.AddScoped<IActivityTimelineService, ActivityTimelineService>();
        services.AddScoped<ITraceLogService, TraceLogService>();
        services.AddScoped<IKpiSnapshotService, KpiSnapshotService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        services.AddHostedService<KpiSnapshotJob>();
        services.AddHostedService<OperationalAlertEvaluatorJob>();

        return services;
    }
}
