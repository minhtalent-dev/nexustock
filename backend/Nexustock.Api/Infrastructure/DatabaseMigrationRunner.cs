using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.MaterialGenealogy.Contexts;
using Nexustock.Modules.Wave.Contexts;
using Serilog;

namespace Nexustock.Api.Infrastructure;

/// <summary>
/// Chạy EF migrate cho toàn bộ module DbContext (một nguồn duy nhất — tránh trùng 2 khối Program.cs).
/// </summary>
public static class DatabaseMigrationRunner
{
    public enum Mode
    {
        /// <summary>Development: lỗi ghi log, không chặn start.</summary>
        Soft,

        /// <summary>--migrate-only: lỗi → success=false để exit 1.</summary>
        FailFast
    }

    public static async Task<bool> MigrateAllAsync(IServiceProvider services, Mode mode)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var success = true;

        async Task RunAsync<TContext>(string name) where TContext : DbContext
        {
            try
            {
                var db = sp.GetRequiredService<TContext>();
                await db.Database.MigrateAsync();
                Log.Information("{Name} database migrated successfully.", name);
            }
            catch (Exception ex)
            {
                if (mode == Mode.FailFast)
                {
                    Log.Error(ex, "An error occurred while migrating the {Name} database", name);
                    success = false;
                }
                else
                {
                    Log.Error(ex, "{Name} migration error", name);
                }
            }
        }

        await RunAsync<Nexustock.Modules.Identity.Contexts.IdentityDbContext>("Identity");
        await RunAsync<Nexustock.Modules.MasterData.Contexts.MasterDataDbContext>("MasterData");
        await RunAsync<Nexustock.Modules.Inbound.Contexts.InboundDbContext>("Inbound");
        await RunAsync<Nexustock.Modules.Qc.Contexts.QcDbContext>("Qc");
        await RunAsync<Nexustock.Modules.Files.Contexts.FilesDbContext>("Files");
        await RunAsync<LpnDbContext>("Lpn");
        await RunAsync<Nexustock.Modules.Inventory.Contexts.InventoryDbContext>("Inventory");
        await RunAsync<Nexustock.Modules.Exceptions.Contexts.ExceptionsDbContext>("Exceptions");
        await RunAsync<Nexustock.Modules.Rules.Contexts.RulesDbContext>("Rules");
        await RunAsync<Nexustock.Modules.Putaway.Contexts.PutawayDbContext>("Putaway");
        await RunAsync<Nexustock.Modules.Replenishment.Contexts.ReplenishmentDbContext>("Replenishment");
        await RunAsync<Nexustock.Modules.Rma.Contexts.RmaDbContext>("Rma");
        await RunAsync<WaveDbContext>("Wave");
        await RunAsync<MaterialGenealogyDbContext>("MaterialGenealogy");
        await RunAsync<Nexustock.Modules.LocalAgent.Contexts.LocalAgentDbContext>("LocalAgent");
        await RunAsync<Nexustock.Modules.LabelPrinting.Contexts.LabelPrintingDbContext>("LabelPrinting");
        await RunAsync<Nexustock.Modules.ErpIntegration.Contexts.ErpIntegrationDbContext>("ErpIntegration");
        await RunAsync<Nexustock.Modules.Webhook.Contexts.WebhookDbContext>("Webhook");
        await RunAsync<Nexustock.Modules.Observability.Contexts.ObservabilityDbContext>("Observability");
        await RunAsync<Nexustock.Modules.CrossDocking.Contexts.CrossDockingDbContext>("CrossDocking");
        await RunAsync<Nexustock.Modules.LaborTracking.Contexts.LaborTrackingDbContext>("LaborTracking");
        await RunAsync<Nexustock.Modules.TaskInterleaving.Contexts.TaskInterleavingDbContext>("TaskInterleaving");
        await RunAsync<Nexustock.Modules.Readiness.Contexts.ReadinessDbContext>("Readiness");

        return success;
    }
}
