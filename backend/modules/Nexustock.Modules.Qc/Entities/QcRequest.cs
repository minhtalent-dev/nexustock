using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Qc.Entities;

public enum QcRequestStatus
{
    Pending,
    Completed,
    Cancelled
}

[Table("qc_requests")]
public class QcRequest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("lot_id")]
    public Guid LotId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("sample_plan")]
    public string SamplePlan { get; set; } = string.Empty;

    [Required]
    [Column("status")]
    public QcRequestStatus Status { get; set; } = QcRequestStatus.Pending;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = "System";

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [ConcurrencyCheck]
    [Column("row_version")]
    public int RowVersion { get; set; } = 1;
}
