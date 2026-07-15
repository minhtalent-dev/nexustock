using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.MaterialGenealogy.Entities;

[Table("genealogy_events", Schema = "genealogy")]
public class GenealogyEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EventType { get; set; } = string.Empty; // SPLIT, MERGE, REPACK, HOLD_BRANCH, RELEASE_BRANCH
    public Guid LotId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Payload { get; set; } // JSON metadata
}
