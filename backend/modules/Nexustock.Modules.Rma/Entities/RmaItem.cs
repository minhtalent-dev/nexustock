using System;

namespace Nexustock.Modules.Rma.Entities;

public class RmaItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RmaId { get; set; }
    public Guid ItemId { get; set; }
    public decimal QtyExpected { get; set; }
    public decimal QtyReceived { get; set; }
    public string? SerialNo { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
