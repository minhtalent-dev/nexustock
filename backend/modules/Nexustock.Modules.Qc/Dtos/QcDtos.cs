using System;

namespace Nexustock.Modules.Qc.Dtos;

public class QcQueueResponseDto
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public string LotNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public decimal ExpectedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RecordQcResultDto
{
    public Guid QcRequestId { get; set; }
    public bool IsPassed { get; set; }
    public string? Metrics { get; set; }
    public string? AttachmentRefs { get; set; }
}

public class HoldLotDto
{
    public Guid? LocationId { get; set; }
    public string ReasonCode { get; set; } = null!;
}

public class ReleaseLotDto
{
    public string ReasonCode { get; set; } = null!;
}

public class RejectLotDto
{
    public string ReasonCode { get; set; } = null!;
}

public class UploadResponseDto
{
    public string Url { get; set; } = null!;
}
