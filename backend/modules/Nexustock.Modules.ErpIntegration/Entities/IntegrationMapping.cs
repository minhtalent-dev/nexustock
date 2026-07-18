using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.ErpIntegration.Entities;

[Table("integration_mappings")]
public class IntegrationMapping
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("external_system")]
    public string ExternalSystem { get; set; } = null!;

    [Required]
    [MaxLength(30)]
    [Column("mapping_type")]
    public string MappingType { get; set; } = null!; // item, warehouse, partner, uom

    [Required]
    [MaxLength(100)]
    [Column("external_code")]
    public string ExternalCode { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    [Column("internal_code")]
    public string InternalCode { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = null!; // active, inactive

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
