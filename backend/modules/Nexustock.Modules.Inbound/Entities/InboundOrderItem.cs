using System;

namespace Nexustock.Modules.Inbound.Entities;

public class InboundOrderItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InboundOrderId { get; set; }
    public Guid ItemId { get; set; }
    public Guid UomId { get; set; }
    public decimal ExpectedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal Tolerance { get; set; }

    public InboundOrder InboundOrder { get; set; } = null!;
}
