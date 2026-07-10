using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Inbound.Dtos;

public class CreateInboundOrderDto
{
    public string? OrderNo { get; set; }
    public Guid PartnerId { get; set; }
    public List<CreateInboundOrderItemDto> Items { get; set; } = new();
}

public class CreateInboundOrderItemDto
{
    public Guid ItemId { get; set; }
    public Guid UomId { get; set; }
    public decimal ExpectedQty { get; set; }
    public decimal Tolerance { get; set; }
}

public class ReceiveItemDto
{
    public Guid ItemId { get; set; }
    public string LotNo { get; set; } = null!;
    public decimal ReceivedQty { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ProductionDate { get; set; }
    public Guid ToLocationId { get; set; }
}

public class InboundOrderResponseDto
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = null!;
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public List<InboundOrderItemResponseDto> Items { get; set; } = new();
}

public class InboundOrderItemResponseDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public Guid UomId { get; set; }
    public string UomName { get; set; } = null!;
    public decimal ExpectedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal Tolerance { get; set; }
}

public class LotResponseDto
{
    public Guid Id { get; set; }
    public string LotNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ProductionDate { get; set; }
    public string QcStatus { get; set; } = null!;
}
