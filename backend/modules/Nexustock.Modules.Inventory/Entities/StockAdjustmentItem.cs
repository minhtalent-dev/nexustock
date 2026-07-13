using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("stock_adjustment_items")]
public class StockAdjustmentItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("adjustment_id")]
    public Guid AdjustmentId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("lot_no")]
    public string LotNo { get; set; } = null!;

    [Column("before_qty")]
    public decimal BeforeQty { get; set; }

    [Column("after_qty")]
    public decimal AfterQty { get; set; }

    [Column("delta_qty")]
    public decimal DeltaQty { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("reason_code")]
    public string ReasonCode { get; set; } = null!;

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
