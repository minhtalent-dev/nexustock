using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Inbound.Entities;

public enum InboundOrderStatus
{
    Draft,
    Open,
    Receiving,
    Completed,
    Cancelled
}

public class InboundOrder
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string OrderNo { get; set; } = null!;
    public Guid PartnerId { get; set; }
    public InboundOrderStatus Status { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public ICollection<InboundOrderItem> Items { get; set; } = new List<InboundOrderItem>();
}
