using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.LocalAgent.Entities;

[Table("AgentPairingCodes")]
public class AgentPairingCode
{
    [Key]
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [Required, MaxLength(50)]
    public string StationCode { get; set; } = null!;
    [Required, MaxLength(100)]
    public string StationName { get; set; } = null!;
    [Required, MaxLength(256)]
    public string CodeHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int InvalidAttempts { get; set; } = 0;
    public bool IsLocked { get; set; } = false;
}
