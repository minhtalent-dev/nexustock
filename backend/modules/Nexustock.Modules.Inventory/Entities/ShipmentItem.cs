using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("shipment_items")]
public class ShipmentItem
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

    [Column("uom_id")]
    public Guid UomId { get; set; }

    [Column("requested_qty")]
    public decimal RequestedQty { get; set; }

    [Column("picked_qty")]
    public decimal PickedQty { get; set; }

    [Column("packed_qty")]
    public decimal PackedQty { get; set; }
}
