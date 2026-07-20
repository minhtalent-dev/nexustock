using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.LaborTracking.Entities;

[Table("labor_session_events")]
public class LaborSessionEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("session_id")]
    public Guid SessionId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("event_type")]
    public string EventType { get; set; } = null!; // Started, Paused, Resumed, Completed, Cancelled, TimedOut

    [Required]
    [MaxLength(100)]
    [Column("actor")]
    public string Actor { get; set; } = null!;

    [Column("occurred_at")]
    public DateTimeOffset OccurredAt { get; set; }

    [Column("payload", TypeName = "jsonb")]
    public string? Payload { get; set; } // Hủy session có lý do, status snapshots, v.v.

    [MaxLength(100)]
    [Column("trace_id")]
    public string? TraceId { get; set; }
}
