using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Putaway.Entities;

[Table("putaway_proposals")]
public class PutawayProposal
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("warehouse_id")]
    public Guid WarehouseId { get; set; }

    [Column("lot_id")]
    public Guid LotId { get; set; }

    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; }

    [Column("candidate_location_id")]
    public Guid CandidateLocationId { get; set; }

    [Column("score")]
    public int Score { get; set; } = 0;

    [MaxLength(250)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "SUGGESTED"; // SUGGESTED, CONFIRMED, REJECTED

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
