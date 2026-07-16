using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("packing_records")]
public class PackingRecord
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

    [Column("weight")]
    public decimal Weight { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("weight_source")]
    public string WeightSource { get; set; } = "scale";

    [Column("scale_stable")]
    public bool ScaleStable { get; set; }

    [Column("manual_override_id")]
    public Guid? ManualOverrideId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Open";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}
