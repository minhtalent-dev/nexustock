using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Replenishment.Entities;

[Table("replenishment_tasks")]
public class ReplenishmentTask
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Column("source_location_id")]
    public Guid SourceLocationId { get; set; } // Bulk Location ID

    [Column("target_location_id")]
    public Guid TargetLocationId { get; set; } // Pick Face Location ID

    [Required]
    [MaxLength(100)]
    [Column("lot_no")]
    public string LotNo { get; set; } = string.Empty;

    [Column("requested_qty")]
    public decimal RequestedQty { get; set; }

    [Column("actual_qty")]
    public decimal? ActualQty { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "PENDING"; // PENDING, ASSIGNED, COMPLETED, CANCELLED

    [Column("mobile_task_id")]
    public Guid? MobileTaskId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = "System";

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Timestamp]
    [Column("xmin", TypeName = "xid")]
    public uint RowVersion { get; set; }
}
