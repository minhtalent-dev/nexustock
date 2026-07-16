using Nexustock.Modules.Inventory.Dtos;
using Nexustock.Modules.Inventory.Entities;

namespace Nexustock.Modules.Inventory.Services;

public interface IWeightValidationService
{
    Task<WeightValidationResult> ValidateAsync(
        CompletePackingRequestDto dto,
        Shipment shipment,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);
}
