using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("location_locks")]
public class LocationLock
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("lock_type")]
    public string LockType { get; set; } = "ALL"; // 'INBOUND', 'OUTBOUND', 'ALL'

    [Required]
    [MaxLength(50)]
    [Column("reason_code")]
    public string ReasonCode { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    [Column("locked_by")]
    public string LockedBy { get; set; } = null!;

    [Column("locked_at")]
    public DateTime LockedAt { get; set; }
}
