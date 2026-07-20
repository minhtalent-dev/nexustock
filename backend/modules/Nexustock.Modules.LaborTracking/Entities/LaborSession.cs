using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.LaborTracking.Entities;

[Table("labor_sessions")]
public class LaborSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("source_task_type")]
    public string SourceTaskType { get; set; } = null!; // MobileTask, PickTask, WavePickTask, Manual

    [Column("source_task_id")]
    public Guid? SourceTaskId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("reference_type")]
    public string ReferenceType { get; set; } = null!;

    [Column("reference_id")]
    public Guid? ReferenceId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("shift_id")]
    public Guid ShiftId { get; set; }

    [Column("location_id")]
    public Guid? LocationId { get; set; }

    [Column("zone_id")]
    public Guid? ZoneId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("operation_type")]
    public string OperationType { get; set; } = null!; // Picking, Putaway, Replenishment, Movement, Packing, Count, Manual

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Running"; // Running, Paused, Completed, Cancelled, TimedOut

    [Column("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [Column("duration_seconds")]
    public int DurationSeconds { get; set; }

    [Column("paused_seconds")]
    public int PausedSeconds { get; set; }

    [Column("last_paused_at")]
    public DateTimeOffset? LastPausedAt { get; set; }

    [Column("timeout_at")]
    public DateTimeOffset? TimeoutAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}
