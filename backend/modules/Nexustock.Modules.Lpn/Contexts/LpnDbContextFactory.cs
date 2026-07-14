using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexustock.Modules.Lpn.Contexts;

public class LpnDbContextFactory : IDesignTimeDbContextFactory<LpnDbContext>
{
    public LpnDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? Environment.GetEnvironmentVariable("DATABASE_CONNECTION")
            ?? "Host=localhost;Port=5435;Database=nexustock;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<LpnDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new LpnDbContext(optionsBuilder.Options);
    }
}
