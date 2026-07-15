using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.MaterialGenealogy.Entities;

[Table("lot_relations", Schema = "genealogy")]
public class LotRelation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ParentLotId { get; set; }
    public Guid ChildLotId { get; set; }
    public string RelationType { get; set; } = "SPLIT"; // SPLIT, MERGE, REPACK
    public decimal QtyTransferred { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
