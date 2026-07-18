using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexustock.Modules.Webhook.Contexts;

/// <summary>
/// Factory dùng cho EF Core design-time tools (dotnet ef migrations add).
/// </summary>
public class WebhookDbContextFactory : IDesignTimeDbContextFactory<WebhookDbContext>
{
    public WebhookDbContext CreateDbContext(string[] args)
    {
        // Ưu tiên env var, fallback về connection string mặc định cho local dev
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nexustock;Username=postgres;Password=password";

        var optionsBuilder = new DbContextOptionsBuilder<WebhookDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new WebhookDbContext(optionsBuilder.Options);
    }
}

