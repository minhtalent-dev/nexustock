using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Exceptions.Entities;

[Table("operational_exceptions")]
public class OperationalException
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("code")]
    public string Code { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("type")]
    public string Type { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    [Column("severity")]
    public string Severity { get; set; } = "MEDIUM";

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Open";

    [Required]
    [MaxLength(50)]
    [Column("reference_type")]
    public string ReferenceType { get; set; } = null!;

    [Column("reference_id")]
    public Guid ReferenceId { get; set; }

    [Column("location_id")]
    public Guid? LocationId { get; set; }

    [MaxLength(100)]
    [Column("lot_no")]
    public string? LotNo { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("reason_code")]
    public string ReasonCode { get; set; } = null!;

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

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

[Table("exception_events")]
public class ExceptionEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("exception_id")]
    public Guid ExceptionId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("transition")]
    public string Transition { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    [Column("actor")]
    public string Actor { get; set; } = null!;

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("exception_assignments")]
public class ExceptionAssignment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("exception_id")]
    public Guid ExceptionId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("owner")]
    public string Owner { get; set; } = null!;

    [Column("sla_deadline")]
    public DateTime? SlaDeadline { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;
}
