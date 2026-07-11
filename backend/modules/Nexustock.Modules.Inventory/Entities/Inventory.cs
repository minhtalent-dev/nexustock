using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("inventories")]
public class Inventory
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

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("qty_on_hand")]
    public decimal QtyOnHand { get; set; }

    [Column("qty_reserved")]
    public decimal QtyReserved { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Column("qty_available")]
    public decimal QtyAvailable { get; set; }

    [ConcurrencyCheck]
    [Column("row_version")]
    public int RowVersion { get; set; } = 1;

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
