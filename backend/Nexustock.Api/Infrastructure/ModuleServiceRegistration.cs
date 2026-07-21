using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Allocation;
using Nexustock.Modules.CrossDocking;
using Nexustock.Modules.ErpIntegration;
using Nexustock.Modules.Exceptions;
using Nexustock.Modules.Identity;
using Nexustock.Modules.Inbound;
using Nexustock.Modules.Inventory;
using Nexustock.Modules.LabelPrinting;
using Nexustock.Modules.LaborTracking;
using Nexustock.Modules.LocalAgent;
using Nexustock.Modules.Lpn;
using Nexustock.Modules.MasterData;
using Nexustock.Modules.MaterialGenealogy;
using Nexustock.Modules.Observability;
using Nexustock.Modules.Putaway;
using Nexustock.Modules.Qc;
using Nexustock.Modules.Readiness;
using Nexustock.Modules.Replenishment;
using Nexustock.Modules.Rma;
using Nexustock.Modules.Rules;
using Nexustock.Modules.Serial;
using Nexustock.Modules.TaskInterleaving;
using Nexustock.Modules.Wave;
using Nexustock.Modules.Webhook;

namespace Nexustock.Api.Infrastructure;

public static class ModuleServiceRegistration
{
    public static IServiceCollection AddNexustockModules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMasterDataModule(configuration);
        services.AddIdentityModule(configuration);
        services.AddInboundModule(configuration);
        services.AddQcModule(configuration);
        services.AddInventoryModule(configuration);
        services.AddExceptionsModule(configuration);
        services.AddRulesModule(configuration);
        services.AddPutawayModule(configuration);
        services.AddAllocationModule(configuration);
        services.AddReplenishmentModule(configuration);
        services.AddLpnModule(configuration);
        services.AddSerialModule(configuration);
        services.AddRmaModule(configuration);
        services.AddWaveModule(configuration);
        services.AddMaterialGenealogyModule(configuration);
        services.AddLocalAgentModule(configuration);
        services.AddLabelPrintingModule(configuration);
        services.AddErpIntegrationModule(configuration);
        services.AddWebhookModule(configuration);
        services.AddObservabilityModule(configuration);
        services.AddCrossDockingModule(configuration);
        services.AddLaborTrackingModule(configuration);
        services.AddTaskInterleavingModule(configuration);
        services.AddReadinessModule(configuration);
        return services;
    }
}
