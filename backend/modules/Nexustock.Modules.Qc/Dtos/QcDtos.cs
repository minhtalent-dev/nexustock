using System;
using System.Collections.Generic;

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
    public double AgingHours { get; set; }
    public string AgingBucket { get; set; } = "fresh"; // fresh | warn24 | critical72
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

public class QcHistoryItemDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!; // RESULT | HOLD | RELEASE
    public Guid LotId { get; set; }
    public string LotNo { get; set; } = null!;
    public string? Inspector { get; set; }
    public bool? IsPassed { get; set; }
    public string? ReasonCode { get; set; }
    public string? Metrics { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QcTimelineEventDto
{
    public string EventType { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string? Actor { get; set; }
    public DateTime At { get; set; }
    public Dictionary<string, string?>? Details { get; set; }
}
