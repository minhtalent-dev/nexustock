using System;

namespace Nexustock.Modules.Wave.Entities;

public class WavePickTask
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WaveId { get; set; }
    public Guid ItemId { get; set; }
    public Guid FromLocationId { get; set; }
    public decimal QtyToPick { get; set; }
    public decimal QtyPicked { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PICKING, COMPLETED
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
