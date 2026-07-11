using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("inventory_movements")]
public class InventoryMovement
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("lot_no")]
    public string LotNo { get; set; } = null!;

    [Column("from_location_id")]
    public Guid FromLocationId { get; set; }

    [Column("to_location_id")]
    public Guid ToLocationId { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Pending"; // 'Pending', 'Completed', 'Cancelled'

    [Required]
    [MaxLength(50)]
    [Column("reason_code")]
    public string ReasonCode { get; set; } = null!;

    [MaxLength(100)]
    [Column("trace_id")]
    public string? TraceId { get; set; }

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
