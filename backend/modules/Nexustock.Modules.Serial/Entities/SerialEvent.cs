using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Serial.Entities;

[Table("serial_events")]
public class SerialEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("serial_id")]
    public Guid SerialId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("event_type")]
    public string EventType { get; set; } = null!; // RECEIVE, QC, PICK, PACK, SHIP, RETURN

    [Column("from_location_id")]
    public Guid? FromLocationId { get; set; }

    [Column("to_location_id")]
    public Guid? ToLocationId { get; set; }

    [Column("reference_id")]
    public Guid? ReferenceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;
}
