using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Nexustock.Modules.Inventory.Dtos;

public class CreateShipmentRequestDto
{
    [Required]
    [MaxLength(100)]
    public string ShipmentNo { get; set; } = null!;

    [Required]
    public Guid PartnerId { get; set; }

    [Required]
    public List<CreateShipmentItemDto> Items { get; set; } = null!;
}

public class CreateShipmentItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    public Guid UomId { get; set; }

    [Required]
    [Range(0.0001, 9999999999)]
    public decimal RequestedQty { get; set; }
}

public class ShipmentListResponseDto
{
    public Guid Id { get; set; }
    public string ShipmentNo { get; set; } = null!;
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class CompletePickRequestDto
{
    [Required]
    [Range(0.0001, 9999999999)]
    public decimal PickedQty { get; set; }
}

public class CompletePackingRequestDto
{
    [Required]
    [MaxLength(100)]
    public string PackageNo { get; set; } = null!;

    [Required]
    [Range(0.0001, 9999999999)]
    public decimal Weight { get; set; }

    [Required]
    [MaxLength(50)]
    public string WeightSource { get; set; } = "scale";

    public bool? ScaleStable { get; set; }

    public Guid? ManualOverrideId { get; set; }
}

public class ManualWeightOverrideRequestDto
{
    [Required]
    public Guid ShipmentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string PackageNo { get; set; } = null!;

    [Required]
    [Range(0.0001, 9999999999)]
    public decimal ManualWeight { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = null!;
}

public class ManualWeightOverrideResponseDto
{
    public Guid ManualOverrideId { get; set; }
    public decimal ManualWeight { get; set; }
    public string WeightSource { get; set; } = "manual_override";
}
