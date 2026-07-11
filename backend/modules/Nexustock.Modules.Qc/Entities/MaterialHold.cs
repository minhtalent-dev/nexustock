using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Qc.Entities;

[Table("material_holds")]
public class MaterialHold
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("lot_id")]
    public Guid LotId { get; set; }

    [Column("location_id")]
    public Guid? LocationId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("reason_code")]
    public string ReasonCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Active"; // Active, Released

    [Required]
    [MaxLength(100)]
    [Column("held_by")]
    public string HeldBy { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("released_by")]
    public string? ReleasedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = "System";

    [Column("released_at")]
    public DateTime? ReleasedAt { get; set; }

    [ConcurrencyCheck]
    [Column("row_version")]
    public int RowVersion { get; set; } = 1;
}
