using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexustock.Modules.LaborTracking.Contexts;

public class LaborTrackingDbContextFactory : IDesignTimeDbContextFactory<LaborTrackingDbContext>
{
    public LaborTrackingDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5435;Database=nexustock_main;Username=kingsman;Password=43zTV!^FiU2g!!nXc3RL!6x2&nw@2V9^BM^@!f8&ersTL!9Sj7";

        var optionsBuilder = new DbContextOptionsBuilder<LaborTrackingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new LaborTrackingDbContext(optionsBuilder.Options);
    }
}
