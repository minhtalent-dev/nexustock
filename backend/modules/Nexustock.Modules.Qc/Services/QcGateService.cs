using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Qc.Abstractions;

namespace Nexustock.Modules.Qc.Services;

public class QcGateService : IQcGateService
{
    private readonly InboundDbContext _inbound;

    public QcGateService(InboundDbContext inbound)
    {
        _inbound = inbound;
    }

    public async Task EnsureLotUsableAsync(Guid tenantId, Guid lotId, CancellationToken ct = default)
    {
        var lot = await _inbound.Lots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lotId && l.TenantId == tenantId, ct);

        EnsureReleased(lot);
    }

    public async Task EnsureLotUsableByLotNoAsync(Guid tenantId, Guid itemId, string lotNo, CancellationToken ct = default)
    {
        var lot = await _inbound.Lots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.TenantId == tenantId && l.ItemId == itemId && l.LotNo == lotNo,
                ct);

        EnsureReleased(lot);
    }

    private static void EnsureReleased(Lot? lot)
    {
        if (lot is null)
        {
            throw new QcGateException(
                "QC_LOT_NOT_FOUND",
                "Lot not found for QC gate check.",
                404);
        }

        if (lot.QcStatus == LotQcStatus.Release)
        {
            return;
        }

        if (lot.QcStatus == LotQcStatus.Hold)
        {
            throw new QcGateException(
                "QC_LOT_ON_HOLD",
                "Lot is on QC hold and cannot be used for warehouse movement.",
                400);
        }

        throw new QcGateException(
            "QC_LOT_NOT_RELEASED",
            $"Lot QC status is {lot.QcStatus}; only Release lots can be used.",
            400);
    }
}
