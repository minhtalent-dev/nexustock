using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("pick_tasks")]
public class PickTask
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("shipment_id")]
    public Guid ShipmentId { get; set; }

    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("lot_no")]
    public string LotNo { get; set; } = null!;

    [Column("from_location_id")]
    public Guid FromLocationId { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; }

    [Column("picked_qty")]
    public decimal PickedQty { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

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
