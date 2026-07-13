using System;

namespace Nexustock.Modules.Exceptions.Entities;

public class OperationalBusinessException : Exception
{
    public string ErrorCode { get; }
    public string Severity { get; }
    public string ReferenceType { get; }
    public Guid ReferenceId { get; }
    public Guid? LocationId { get; }
    public string? LotNo { get; }
    public decimal Qty { get; set; }

    public OperationalBusinessException(
        string message,
        string errorCode,
        string severity,
        string referenceType,
        Guid referenceId,
        Guid? locationId = null,
        string? lotNo = null,
        decimal qty = 0) : base(message)
    {
        ErrorCode = errorCode;
        Severity = severity;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        LocationId = locationId;
        LotNo = lotNo;
        Qty = qty;
    }
}
