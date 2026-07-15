using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace Nexustock.Modules.Serial.Contexts;

public class SerialDbContextFactory : IDesignTimeDbContextFactory<SerialDbContext>
{
    public SerialDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? Environment.GetEnvironmentVariable("DATABASE_CONNECTION")
            ?? "Host=localhost;Port=5435;Database=nexustock;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<SerialDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SerialDbContext(optionsBuilder.Options);
    }
}
