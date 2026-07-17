namespace Nexustock.Modules.LabelPrinting.DTOs;

public sealed record OperationResult(bool Success, string? ErrorCode = null, string? Message = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string errorCode, string message) => new(false, errorCode, message);
}

public sealed record PrintJobDto(
    Guid Id,
    Guid TemplateId,
    string TemplateCode,
    string PrinterCode,
    string Status,
    string IdempotencyKey,
    string RenderedCommandHash,
    Guid? SourceJobId,
    string? ReasonCode,
    int ReprintCount,
    DateTimeOffset CreatedAt,
    string? ErrorMessage);

public sealed record CreatePrintJobRequest(
    Guid TemplateId,
    string PrinterCode,
    Dictionary<string, string> Payload,
    string IdempotencyKey);

public sealed record ReprintJobRequest(
    string ReasonCode,
    string IdempotencyKey);
