using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("stocktake_items")]
public class StocktakeItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("stocktake_id")]
    public Guid StocktakeId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("lot_no")]
    public string LotNo { get; set; } = null!;

    [Column("system_qty")]
    public decimal SystemQty { get; set; }

    [Column("counted_qty")]
    public decimal? CountedQty { get; set; }

    [Column("variance_qty")]
    public decimal? VarianceQty { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Pending"; // 'Pending', 'Counted', 'RecountRequested'

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
