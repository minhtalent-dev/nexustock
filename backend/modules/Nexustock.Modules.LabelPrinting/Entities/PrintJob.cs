using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.LabelPrinting.Entities;

[Table("print_jobs")]
public class PrintJob
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("template_id")]
    public Guid TemplateId { get; set; }

    [Required, MaxLength(80)]
    [Column("printer_code")]
    public string PrinterCode { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = "queued";

    [Required, MaxLength(120)]
    [Column("idempotency_key")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    [Column("payload_json")]
    public string PayloadJson { get; set; } = "{}";

    [Required]
    [Column("rendered_command")]
    public string RenderedCommand { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    [Column("rendered_command_hash")]
    public string RenderedCommandHash { get; set; } = string.Empty;

    [Column("source_job_id")]
    public Guid? SourceJobId { get; set; }

    [MaxLength(50)]
    [Column("reason_code")]
    public string? ReasonCode { get; set; }

    [Column("reprint_count")]
    public int ReprintCount { get; set; }

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

    [MaxLength(500)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [ForeignKey("TemplateId")]
    public virtual LabelTemplate Template { get; set; } = null!;
}
