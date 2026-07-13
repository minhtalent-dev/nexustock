using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Nexustock.Modules.Inventory.Dtos;

public class StocktakeListResponseDto
{
    public Guid Id { get; set; }
    public string StocktakeNo { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public decimal TotalVarianceAmount { get; set; }
    public int CurrentApprovalLevel { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? StartedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class CreateStocktakeRequestDto
{
    [Required]
    [MaxLength(100)]
    public string StocktakeNo { get; set; } = null!;
    public Guid? ZoneId { get; set; }
    public List<Guid>? LocationIds { get; set; }
}

public class RecordCountRequestDto
{
    [Required]
    public Guid LocationId { get; set; }
    [Required]
    public Guid ItemId { get; set; }
    [Required]
    [MaxLength(100)]
    public string LotNo { get; set; } = null!;
    [Required]
    [Range(0, 9999999999)]
    public decimal CountedQty { get; set; }
}

public class ApproveStocktakeRequestDto
{
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;
    [MaxLength(500)]
    public string? Remarks { get; set; }
}
