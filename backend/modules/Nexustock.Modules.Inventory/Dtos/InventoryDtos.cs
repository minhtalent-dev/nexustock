using System;
using System.ComponentModel.DataAnnotations;

namespace Nexustock.Modules.Inventory.Dtos;

public class InventoryBalanceResponseDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string LotNo { get; set; } = null!;
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = null!;
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtyAvailable { get; set; }
}

public class MoveInventoryRequestDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    public string LotNo { get; set; } = null!;

    [Required]
    public Guid FromLocationId { get; set; }

    [Required]
    public Guid ToLocationId { get; set; }

    [Required]
    [Range(0.0001, 9999999999)]
    public decimal Qty { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;
}

public class LockLocationRequestDto
{
    [Required]
    public string LockType { get; set; } = "ALL"; // 'INBOUND', 'OUTBOUND', 'ALL'

    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;
}
