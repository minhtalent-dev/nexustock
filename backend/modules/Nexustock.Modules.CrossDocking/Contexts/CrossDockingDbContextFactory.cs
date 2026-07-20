using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexustock.Modules.CrossDocking.Contexts;

public class CrossDockingDbContextFactory : IDesignTimeDbContextFactory<CrossDockingDbContext>
{
    public CrossDockingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nexustock_main;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<CrossDockingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CrossDockingDbContext(optionsBuilder.Options);
    }
}
