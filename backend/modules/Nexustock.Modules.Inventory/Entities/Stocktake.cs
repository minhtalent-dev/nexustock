using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("stocktakes")]
public class Stocktake
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("stocktake_no")]
    public string StocktakeNo { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Draft"; // 'Draft', 'Counting', 'Pending_L1_Approve', 'Pending_L2_Approve', 'Pending_L3_Approve', 'Approved', 'Cancelled'

    [Column("zone_id")]
    public Guid? ZoneId { get; set; }

    [Column("total_variance_amount")]
    public decimal TotalVarianceAmount { get; set; } = 0.0000m;

    [Column("current_approval_level")]
    public int CurrentApprovalLevel { get; set; } = 0; // 0: counting/draft, 1: L1, 2: L2, 3: L3

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [MaxLength(100)]
    [Column("started_by")]
    public string? StartedBy { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(100)]
    [Column("completed_by")]
    public string? CompletedBy { get; set; }

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
