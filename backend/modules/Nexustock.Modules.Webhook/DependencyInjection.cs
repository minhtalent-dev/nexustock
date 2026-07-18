using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Webhook.Contexts;
using Nexustock.Modules.Webhook.Services;
using Nexustock.Modules.Webhook.Workers;

namespace Nexustock.Modules.Webhook;

public static class DependencyInjection
{
    public static IServiceCollection AddWebhookModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WebhookDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddHttpClient();
        services.AddScoped<IWebhookOutboxService, WebhookOutboxService>();
        services.AddScoped<IWebhookSigningService, WebhookSigningService>();

        // Background Worker quét và gửi Webhook Outbox
        services.AddHostedService<WebhookOutboxWorker>();

        return services;
    }
}
