using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("manual_weight_overrides")]
public class ManualWeightOverride
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("shipment_id")]
    public Guid ShipmentId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("package_no")]
    public string PackageNo { get; set; } = null!;

    [Column("manual_weight")]
    public decimal ManualWeight { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("reason")]
    public string Reason { get; set; } = null!;

    [Column("approved_by")]
    public Guid ApprovedBy { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;
}
