using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.LocalAgent.Entities;

[Table("AgentConnectionEvents")]
public class AgentConnectionEvent
{
    [Key]
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? StationId { get; set; }
    [Required, MaxLength(50)]
    public string EventType { get; set; } = null!;
    [MaxLength(300)]
    public string? Origin { get; set; }
    [MaxLength(100)]
    public string? MachineName { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }

    [ForeignKey("StationId")]
    public virtual AgentStation? Station { get; set; }
}
