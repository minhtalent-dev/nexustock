using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Dtos;
using Nexustock.Modules.Inventory.Entities;

namespace Nexustock.Modules.Inventory.Services;

public sealed class WeightValidationService : IWeightValidationService
{
    private readonly InventoryDbContext _context;

    public WeightValidationService(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<WeightValidationResult> ValidateAsync(
        CompletePackingRequestDto dto,
        Shipment shipment,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (dto.Weight <= 0)
        {
            return Fail("WEIGHT_INVALID", "Cân nặng phải lớn hơn 0.");
        }

        if (dto.WeightSource == WeightSources.Scale)
        {
            if (dto.ScaleStable != true)
            {
                return Fail("SCALE_WEIGHT_UNSTABLE", "Cân chưa ổn định. Vui lòng chờ tín hiệu ổn định từ cân.");
            }

            return new WeightValidationResult(true, dto.Weight, WeightSources.Scale, true, null, null, null);
        }

        if (dto.WeightSource != WeightSources.ManualOverride)
        {
            return Fail("WEIGHT_SOURCE_INVALID", "Nguồn cân nặng không hợp lệ.");
        }

        if (!dto.ManualOverrideId.HasValue)
        {
            return Fail("MANUAL_OVERRIDE_REQUIRED", "Thiếu phiếu duyệt nhập cân nặng thủ công.");
        }

        var manualOverride = await _context.ManualWeightOverrides.FirstOrDefaultAsync(o =>
            o.Id == dto.ManualOverrideId.Value &&
            o.TenantId == tenantId &&
            o.ShipmentId == shipment.Id &&
            o.PackageNo == dto.PackageNo &&
            o.UsedAt == null,
            cancellationToken);

        if (manualOverride == null)
        {
            return Fail("MANUAL_OVERRIDE_NOT_FOUND", "Phiếu duyệt nhập cân nặng thủ công không hợp lệ hoặc đã sử dụng.");
        }

        manualOverride.UsedAt = DateTime.UtcNow;
        return new WeightValidationResult(true, manualOverride.ManualWeight, WeightSources.ManualOverride, false, manualOverride.Id, null, null);
    }

    private static WeightValidationResult Fail(string errorCode, string message)
    {
        return new WeightValidationResult(false, 0m, string.Empty, false, null, errorCode, message);
    }
}
