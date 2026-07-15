using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.MasterData.Entities;

[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(100)]
    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("base_uom_id")]
    public Guid BaseUomId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_serial_tracked")]
    public bool IsSerialTracked { get; set; } = false;

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

    [ForeignKey("BaseUomId")]
    public virtual Uom? BaseUom { get; set; }
}
