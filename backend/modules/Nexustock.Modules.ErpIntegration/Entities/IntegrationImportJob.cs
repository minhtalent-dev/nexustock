using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.ErpIntegration.Entities;

[Table("integration_import_jobs")]
public class IntegrationImportJob
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("import_type")]
    public string ImportType { get; set; } = null!; // items, mappings, inboundOrders

    [Required]
    [MaxLength(255)]
    [Column("file_name")]
    public string FileName { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = null!; // previewed, committed, failed, expired

    [Column("total_rows")]
    public int TotalRows { get; set; }

    [Column("valid_rows")]
    public int ValidRows { get; set; }

    [Column("error_rows")]
    public int ErrorRows { get; set; }

    [Required]
    [Column("preview_payload")]
    public string PreviewPayload { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("trace_id")]
    public string TraceId { get; set; } = null!;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }
}
