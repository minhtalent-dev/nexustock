using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Lpn.Entities;

[Table("lpn_events")]
public class LpnEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("lpn_id")]
    public Guid LpnId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("event_type")]
    public string EventType { get; set; } = null!; // CREATE, ATTACH, DETACH, MOVE, SHIP, EMPTY

    [Column("item_id")]
    public Guid? ItemId { get; set; }

    [MaxLength(100)]
    [Column("lot_no")]
    public string? LotNo { get; set; }

    [Column("qty")]
    public decimal? Qty { get; set; }

    [Column("from_location_id")]
    public Guid? FromLocationId { get; set; }

    [Column("to_location_id")]
    public Guid? ToLocationId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;
}
