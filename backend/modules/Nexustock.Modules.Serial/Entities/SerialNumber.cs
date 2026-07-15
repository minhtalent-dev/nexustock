using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Serial.Entities;

[Table("serial_numbers")]
public class SerialNumber
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
    [Column("serial_no")]
    public string SerialNo { get; set; } = null!;

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "ACTIVE"; // RECEIVED, ACTIVE, PICKED, SHIPPED, LOCKED

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

    [Timestamp]
    [Column("xmin", TypeName = "xid")]
    public uint RowVersion { get; set; }
}
