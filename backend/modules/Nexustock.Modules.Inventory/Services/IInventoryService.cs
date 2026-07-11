using System;
using System.Threading.Tasks;

namespace Nexustock.Modules.Inventory.Services;

public interface IInventoryService
{
    Task RecordReceiptAsync(
        Guid tenantId,
        Guid itemId,
        string lotNo,
        Guid toLocationId,
        decimal qty,
        string username,
        string traceId);
}
