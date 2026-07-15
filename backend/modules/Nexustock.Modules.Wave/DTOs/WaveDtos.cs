using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Wave.DTOs;

public class CreateWaveDto
{
    public List<Guid> ShipmentIds { get; set; } = new();
}

public class WaveListDto
{
    public Guid Id { get; set; }
    public string WaveNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalQty { get; set; }
}

public class WaveDetailDto
{
    public Guid Id { get; set; }
    public string WaveNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<WaveItemDetailDto> Items { get; set; } = new();
    public List<WavePickTaskDto> PickTasks { get; set; } = new();
}

public class WaveItemDetailDto
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string ShipmentNo { get; set; } = string.Empty;
    public Guid ShipmentItemId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string UomName { get; set; } = string.Empty;
    public decimal QtyExpected { get; set; }
    public decimal QtyAllocated { get; set; }
    public decimal QtyPicked { get; set; }
    public decimal QtySorted { get; set; }
    public int? RecommendedSlotNumber { get; set; }
}

public class WavePickTaskDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public Guid FromLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public decimal QtyToPick { get; set; }
    public decimal QtyPicked { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CompleteWavePickDto
{
    public Guid TaskId { get; set; }
    public decimal PickedQty { get; set; }
    public List<string>? SerialNos { get; set; }
}

public class SortRequestDto
{
    public string BarcodeOrSerial { get; set; } = string.Empty;
}

public class SortResponseDto
{
    public Guid ShipmentId { get; set; }
    public string ShipmentNo { get; set; } = string.Empty;
    public int RecommendedSlotNumber { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public decimal QtySorted { get; set; }
    public decimal QtyExpected { get; set; }
    public bool IsSlotComplete { get; set; }
}
