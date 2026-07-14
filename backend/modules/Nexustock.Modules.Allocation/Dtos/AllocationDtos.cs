using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Allocation.Dtos;

public class ReserveRequestDto
{
    public Guid ShipmentId { get; set; }
    public string Strategy { get; set; } = "FEFO"; // FEFO, FIFO
    public bool AllowPartial { get; set; } = true;
    public int ReservationTtlMinutes { get; set; } = 1440;
}

public class ReserveResponseDto
{
    public bool Success { get; set; }
    public Guid ShipmentId { get; set; }
    public string Status { get; set; } = null!; // ALLOCATED, PARTIALLY_ALLOCATED, FAILED
    public List<AllocatedLineDto> AllocatedLines { get; set; } = new();
    public string Message { get; set; } = null!;
}

public class AllocatedLineDto
{
    public Guid ShipmentLineId { get; set; }
    public Guid ItemId { get; set; }
    public decimal RequestedQty { get; set; }
    public decimal AllocatedQty { get; set; }
    public List<ReservationDetailDto> Reservations { get; set; } = new();
}

public class ReservationDetailDto
{
    public Guid ReservationId { get; set; }
    public string LocationCode { get; set; } = null!;
    public string LotNo { get; set; } = null!;
    public decimal Qty { get; set; }
}

public class ReleaseRequestDto
{
    public Guid ShipmentId { get; set; }
}

public class AvailabilityResponseDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtyAvailable { get; set; }
}
