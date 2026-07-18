using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.ErpIntegration.Contexts;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.Modules.ErpIntegration.Services;

public class IntegrationMappingService : IIntegrationMappingService
{
    private readonly ErpIntegrationDbContext _context;
    private readonly MasterDataDbContext _masterContext;

    public IntegrationMappingService(ErpIntegrationDbContext context, MasterDataDbContext masterContext)
    {
        _context = context;
        _masterContext = masterContext;
    }

    private async Task<string> ResolveInternalCodeAsync(Guid tenantId, string externalSystem, string mappingType, string externalCode)
    {
        // First check if there is an active mapping in IntegrationMappings
        var mapping = await _context.IntegrationMappings
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && 
                                      m.ExternalSystem == externalSystem && 
                                      m.MappingType == mappingType && 
                                      m.ExternalCode == externalCode &&
                                      m.Status == "active");

        if (mapping != null)
        {
            return mapping.InternalCode;
        }

        // Default fallback: if no mapping, check if the externalCode matches a WMS internal code directly
        return externalCode;
    }

    public async Task<Guid> ResolveItemAsync(Guid tenantId, string externalSystem, string externalCode)
    {
        var internalCode = await ResolveInternalCodeAsync(tenantId, externalSystem, "item", externalCode);
        
        var product = await _masterContext.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == internalCode && p.IsActive);

        if (product == null)
        {
            throw new UnresolvedMappingException(
                "mapping.unresolvedItemCode", 
                externalCode, 
                $"Unresolved item mapping for external code {externalCode} (resolved to {internalCode})");
        }

        return product.Id;
    }

    public async Task<Guid> ResolveWarehouseAsync(Guid tenantId, string externalSystem, string externalCode)
    {
        var internalCode = await ResolveInternalCodeAsync(tenantId, externalSystem, "warehouse", externalCode);

        var warehouse = await _masterContext.Warehouses
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Code == internalCode && w.IsActive);

        if (warehouse == null)
        {
            throw new UnresolvedMappingException(
                "mapping.unresolvedWarehouse", 
                externalCode, 
                $"Unresolved warehouse mapping for external code {externalCode} (resolved to {internalCode})");
        }

        return warehouse.Id;
    }

    public async Task<Guid> ResolvePartnerAsync(Guid tenantId, string externalSystem, string externalCode)
    {
        var internalCode = await ResolveInternalCodeAsync(tenantId, externalSystem, "partner", externalCode);

        var partner = await _masterContext.Partners
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Code == internalCode && p.IsActive);

        if (partner == null)
        {
            throw new UnresolvedMappingException(
                "mapping.unresolvedPartner", 
                externalCode, 
                $"Unresolved partner mapping for external code {externalCode} (resolved to {internalCode})");
        }

        return partner.Id;
    }

    public async Task<Guid> ResolveUomAsync(Guid tenantId, string externalSystem, string externalCode)
    {
        var internalCode = await ResolveInternalCodeAsync(tenantId, externalSystem, "uom", externalCode);

        var uom = await _masterContext.Uoms
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Code == internalCode && u.IsActive);

        if (uom == null)
        {
            throw new UnresolvedMappingException(
                "mapping.unresolvedUom", 
                externalCode, 
                $"Unresolved UOM mapping for external code {externalCode} (resolved to {internalCode})");
        }

        return uom.Id;
    }
}
