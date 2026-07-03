using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.MasterData.Entities;

[Table("storage_locations")]
public class StorageLocation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("zone_id")]
    public Guid ZoneId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("max_capacity")]
    public decimal MaxCapacity { get; set; } = 999999.0000m;

    [Column("max_volume")]
    public decimal MaxVolume { get; set; } = 999999.0000m;

    [Column("x_coord")]
    public int XCoord { get; set; } = 0;

    [Column("y_coord")]
    public int YCoord { get; set; } = 0;

    [Column("z_coord")]
    public int ZCoord { get; set; } = 0;

    [Column("length")]
    public decimal Length { get; set; } = 0.00m;

    [Column("width")]
    public decimal Width { get; set; } = 0.00m;

    [Column("height")]
    public decimal Height { get; set; } = 0.00m;

    [Column("is_locked")]
    public bool IsLocked { get; set; } = false;

    [MaxLength(50)]
    [Column("lock_reason_code")]
    public string? LockReasonCode { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(100)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [ConcurrencyCheck]
    [Column("row_version")]
    public int RowVersion { get; set; } = 1;

    [ForeignKey("TenantId")]
    public virtual Tenant? Tenant { get; set; }

    [ForeignKey("ZoneId")]
    public virtual StorageZone? Zone { get; set; }
}
