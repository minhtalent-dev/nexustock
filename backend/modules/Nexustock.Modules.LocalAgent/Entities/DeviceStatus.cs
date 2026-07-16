using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.LocalAgent.Entities;

[Table("DeviceStatuses")]
public class DeviceStatus
{
    [Key]
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StationId { get; set; }
    [Required, MaxLength(50)]
    public string DeviceId { get; set; } = null!;
    [Required, MaxLength(30)]
    public string DeviceType { get; set; } = null!;
    [Required, MaxLength(20)]
    public string ConnectionState { get; set; } = "disconnected";
    public DateTime LastHeartbeatAt { get; set; }
    public string? LastErrorMessage { get; set; }

    [ForeignKey("StationId")]
    public virtual AgentStation Station { get; set; } = null!;
}
