using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.ErpIntegration.Entities;

[Table("integration_messages")]
public class IntegrationMessage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("idempotency_key")]
    public string IdempotencyKey { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    [Column("payload_hash")]
    public string PayloadHash { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("external_system")]
    public string ExternalSystem { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    [Column("external_reference")]
    public string ExternalReference { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    [Column("contract_version")]
    public string ContractVersion { get; set; } = null!;

    [Required]
    [MaxLength(10)]
    [Column("direction")]
    public string Direction { get; set; } = null!; // inbound, outbound

    [Required]
    [MaxLength(50)]
    [Column("message_type")]
    public string MessageType { get; set; } = null!; // purchaseOrder, salesOrder, stockUpdate

    [Required]
    [Column("payload")]
    public string Payload { get; set; } = null!;

    [Column("response_payload")]
    public string? ResponsePayload { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = null!; // accepted, failed, conflict

    [MaxLength(100)]
    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("trace_id")]
    public string TraceId { get; set; } = null!;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
