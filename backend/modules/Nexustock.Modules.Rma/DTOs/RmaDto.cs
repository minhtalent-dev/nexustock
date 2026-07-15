using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Rma.DTOs;

public class RmaDto
{
    public Guid Id { get; set; }
    public string RmaNo { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? ReferenceNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<RmaItemDto> Items { get; set; } = new();
}

public class RmaItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QtyExpected { get; set; }
    public decimal QtyReceived { get; set; }
    public string? SerialNo { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
}

public class CreateRmaDto
{
    public Guid CustomerId { get; set; }
    public string? ReferenceNo { get; set; }
    public List<CreateRmaItemDto> Items { get; set; } = new();
}

public class CreateRmaItemDto
{
    public Guid ItemId { get; set; }
    public decimal QtyExpected { get; set; }
    public string? SerialNo { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
}

public class ReceiveRmaDto
{
    public List<ReceiveRmaItemDto> Items { get; set; } = new();
}

public class ReceiveRmaItemDto
{
    public Guid ItemId { get; set; }
    public decimal QtyReceived { get; set; }
    public string? SerialNo { get; set; }
}

public class ProcessRmaQcDto
{
    public List<ProcessRmaQcItemDto> Results { get; set; } = new();
}

public class ProcessRmaQcItemDto
{
    public Guid RmaItemId { get; set; }
    public string QcStatus { get; set; } = string.Empty; // PASS, FAIL
    public string Disposition { get; set; } = string.Empty; // RESTOCK, QUARANTINE, SCRAP
    public decimal Qty { get; set; }
    public string? Notes { get; set; }
}
