using System.ComponentModel.DataAnnotations;

namespace Nexustock.Modules.MaterialGenealogy.DTOs;

public class CreateLotRelationDto
{
    [Required] public string ParentLotNo { get; set; } = string.Empty;
    [Required] public string ChildLotNo { get; set; } = string.Empty;
    [Required] public string RelationType { get; set; } = "SPLIT";
    [Range(0.0001, 999999999)] public decimal QtyTransferred { get; set; }
    public List<string> SerialNos { get; set; } = new();
}

public class LotGenealogyNodeDto
{
    public Guid LotId { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QtyOnHand { get; set; }
    public string Status { get; set; } = "RELEASED";
    public List<LotGenealogyNodeDto> Children { get; set; } = new();
    public List<LotGenealogyNodeDto> Parents { get; set; } = new();
}

public class HoldBranchDto
{
    [Required] public string TargetLotNo { get; set; } = string.Empty;
    [Required] public string ReasonCode { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
}
