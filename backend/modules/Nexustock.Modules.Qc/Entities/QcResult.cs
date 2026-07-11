using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Qc.Entities;

[Table("qc_results")]
public class QcResult
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("qc_request_id")]
    public Guid QcRequestId { get; set; }

    [Column("is_passed")]
    public bool IsPassed { get; set; }

    [Column("metrics")]
    public string? Metrics { get; set; }

    [Column("attachment_refs")]
    public string? AttachmentRefs { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("inspector")]
    public string Inspector { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = "System";

    [ForeignKey("QcRequestId")]
    public virtual QcRequest? QcRequest { get; set; }
}
