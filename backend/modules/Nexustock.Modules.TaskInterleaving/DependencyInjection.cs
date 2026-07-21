using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.TaskInterleaving.Contexts;

namespace Nexustock.Modules.TaskInterleaving;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskInterleavingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<TaskInterleavingDbContext>(options =>
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(TaskInterleavingDbContext).Assembly.FullName)));

        services.AddScoped<Services.ITaskInterleavingService, Services.TaskInterleavingService>();

        return services;
    }
}
