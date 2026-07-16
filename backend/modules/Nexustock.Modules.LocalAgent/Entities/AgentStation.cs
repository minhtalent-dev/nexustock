using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.LocalAgent.Entities;

[Table("AgentStations")]
public class AgentStation
{
    [Key]
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [Required, MaxLength(50)]
    public string StationCode { get; set; } = null!;
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;
    [Required, MaxLength(256)]
    public string TokenHash { get; set; } = null!;
    [Required, MaxLength(30)]
    public string Status { get; set; } = "active"; // "active", "revoked"
    [MaxLength(100)]
    public string? MachineName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
