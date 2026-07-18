using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Observability.Entities;

public class FeatureFlag
{
    [Key]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public bool Enabled { get; set; }

    public int RolloutPercentage { get; set; }

    public string? WhitelistUserIds { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
