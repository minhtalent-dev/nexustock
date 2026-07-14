using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Replenishment.Entities;

[Table("replenishment_rules")]
public class ReplenishmentRule
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; } // Pick Face Location ID

    [Column("min_qty")]
    public decimal MinQty { get; set; }

    [Column("max_qty")]
    public decimal MaxQty { get; set; }

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
