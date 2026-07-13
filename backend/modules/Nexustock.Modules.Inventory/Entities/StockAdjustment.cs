using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("stock_adjustments")]
public class StockAdjustment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("stocktake_id")]
    public Guid StocktakeId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("adjustment_no")]
    public string AdjustmentNo { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Applied"; // 'Pending', 'Applied', 'Rejected'

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [MaxLength(100)]
    [Column("approved_by")]
    public string? ApprovedBy { get; set; }

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
