using System;

namespace Nexustock.Modules.Inbound.Entities;

public class InventoryTransaction
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public string LotNo { get; set; } = null!;
    public string TransactionType { get; set; } = "RECEIVE";
    public decimal Qty { get; set; }
    public Guid ToLocationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? TraceId { get; set; }
}
