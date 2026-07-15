using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.MaterialGenealogy.Contexts;
using Nexustock.Modules.MaterialGenealogy.Services;

namespace Nexustock.Modules.MaterialGenealogy;

public static class DependencyInjection
{
    public static IServiceCollection AddMaterialGenealogyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MaterialGenealogyDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IMaterialGenealogyService, MaterialGenealogyService>();

        return services;
    }
}
