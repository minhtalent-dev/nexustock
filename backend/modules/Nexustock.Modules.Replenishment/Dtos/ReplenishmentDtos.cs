using System;

namespace Nexustock.Modules.Replenishment.Dtos;

public class CreateReplenishmentRuleDto
{
    public Guid ItemId { get; set; }
    public Guid LocationId { get; set; }
    public decimal MinQty { get; set; }
    public decimal MaxQty { get; set; }
}

public class ReplenishmentRuleResponseDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Guid LocationId { get; set; }
    public decimal MinQty { get; set; }
    public decimal MaxQty { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class ReplenishmentTaskResponseDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Guid SourceLocationId { get; set; }
    public Guid TargetLocationId { get; set; }
    public string LotNo { get; set; } = null!;
    public decimal RequestedQty { get; set; }
    public decimal? ActualQty { get; set; }
    public string Status { get; set; } = null!;
    public Guid? MobileTaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class CompleteReplenishmentTaskDto
{
    public decimal ActualQty { get; set; }
    public string OperatorName { get; set; } = null!;
}
