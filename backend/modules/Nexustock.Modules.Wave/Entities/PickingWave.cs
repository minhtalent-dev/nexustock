using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Wave.Entities;

public class PickingWave
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string WaveNo { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT"; // DRAFT, RELEASED, SORTING, COMPLETED
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public int RowVersion { get; set; } = 1;

    public List<WaveItem> Items { get; set; } = new();
}
