using System;

namespace Nexustock.Modules.Wave.Entities;

public class WaveItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WaveId { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid ShipmentItemId { get; set; }
    public Guid ItemId { get; set; }
    public decimal QtyExpected { get; set; }
    public decimal QtyAllocated { get; set; }
    public decimal QtyPicked { get; set; }
    public decimal QtySorted { get; set; }
}
