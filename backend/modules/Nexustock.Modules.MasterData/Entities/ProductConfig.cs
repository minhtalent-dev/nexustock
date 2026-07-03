using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.MasterData.Entities;

[Table("product_configs")]
public class ProductConfig
{
    [Key]
    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("iqc_check_type")]
    public string IqcCheckType { get; set; } = "FULL"; // FULL, SAMPLE, NONE

    [Column("vendor_inner_lot_ctl")]
    public bool VendorInnerLotCtl { get; set; } = false;

    [Column("is_wafer")]
    public bool IsWafer { get; set; } = false;

    [MaxLength(255)]
    [Column("lot_validation_regex")]
    public string? LotValidationRegex { get; set; }

    [Column("min_stock")]
    public decimal MinStock { get; set; } = 0.0000m;

    [Column("max_stock")]
    public decimal MaxStock { get; set; } = 999999.0000m;

    [Required]
    [MaxLength(20)]
    [Column("weight_class")]
    public string WeightClass { get; set; } = "MEDIUM"; // LIGHT, MEDIUM, HEAVY

    [Required]
    [MaxLength(20)]
    [Column("rotation_speed")]
    public string RotationSpeed { get; set; } = "SLOW"; // SLOW, MEDIUM, FAST

    [Column("track_serial")]
    public bool TrackSerial { get; set; } = false;

    [Column("length")]
    public decimal Length { get; set; } = 0.00m; // mm

    [Column("width")]
    public decimal Width { get; set; } = 0.00m;  // mm

    [Column("height")]
    public decimal Height { get; set; } = 0.00m; // mm

    [Column("weight")]
    public decimal Weight { get; set; } = 0.00m; // g

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }

    [ForeignKey("TenantId")]
    public virtual Tenant? Tenant { get; set; }
}
