using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.LabelPrinting.Contexts;
using Nexustock.Modules.LabelPrinting.DTOs;
using Nexustock.Modules.LabelPrinting.Entities;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.Modules.LabelPrinting.Services;

public interface ILabelPrintingService
{
    Task<(OperationResult Result, PrintJobDto? Item)> CreateJobAsync(CreatePrintJobRequest request, string username, CancellationToken cancellationToken);
    Task<(OperationResult Result, PrintJobDto? Item)> ReprintJobAsync(Guid id, ReprintJobRequest request, string username, CancellationToken cancellationToken);
}

public class LabelPrintingService : ILabelPrintingService
{
    private const int MaxReprintCount = 3;
    private readonly LabelPrintingDbContext _db;
    private readonly MasterDataDbContext _masterDb;
    private readonly LabelTemplateRenderer _renderer;

    public LabelPrintingService(LabelPrintingDbContext db, MasterDataDbContext masterDb, LabelTemplateRenderer renderer)
    {
        _db = db;
        _masterDb = masterDb;
        _renderer = renderer;
    }

    public async Task<(OperationResult Result, PrintJobDto? Item)> CreateJobAsync(CreatePrintJobRequest request, string username, CancellationToken cancellationToken)
    {
        var validation = ValidateCreateRequest(request);
        if (!validation.Success) return (validation, null);

        var existing = await _db.PrintJobs
            .AsNoTracking()
            .Include(x => x.Template)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return (OperationResult.Ok(), ToDto(existing));

        var template = await _db.LabelTemplates.FirstOrDefaultAsync(x => x.Id == request.TemplateId && x.IsActive, cancellationToken);
        if (template is null) return (OperationResult.Fail("TEMPLATE_NOT_FOUND", "Không tìm thấy mẫu tem đang hoạt động."), null);

        string renderedCommand;
        try
        {
            renderedCommand = _renderer.Render(template.RawTemplate, request.Payload, template.Language);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return (OperationResult.Fail("RENDER_FAILED", ex.Message), null);
        }

        var job = new PrintJob
        {
            Id = Guid.NewGuid(),
            TenantId = _db.CurrentTenantId,
            TemplateId = template.Id,
            PrinterCode = NormalizeCode(request.PrinterCode),
            Status = "queued",
            IdempotencyKey = request.IdempotencyKey.Trim(),
            PayloadJson = JsonSerializer.Serialize(request.Payload),
            RenderedCommand = renderedCommand,
            RenderedCommandHash = LabelTemplateRenderer.ComputeCommandHash(renderedCommand),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = username
        };

        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        job.Template = template;

        return (OperationResult.Ok(), ToDto(job));
    }

    public async Task<(OperationResult Result, PrintJobDto? Item)> ReprintJobAsync(Guid id, ReprintJobRequest request, string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReasonCode)) return (OperationResult.Fail("REASON_CODE_REQUIRED", "Bắt buộc chọn lý do in lại."), null);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) return (OperationResult.Fail("IDEMPOTENCY_KEY_REQUIRED", "Bắt buộc truyền khóa chống gửi trùng."), null);

        var existing = await _db.PrintJobs
            .AsNoTracking()
            .Include(x => x.Template)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return (OperationResult.Ok(), ToDto(existing));

        var source = await _db.PrintJobs
            .Include(x => x.Template)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (source is null) return (OperationResult.Fail("JOB_NOT_FOUND", "Không tìm thấy lệnh in gốc."), null);
        if (source.ReprintCount >= MaxReprintCount) return (OperationResult.Fail("REPRINT_LIMIT_EXCEEDED", "Lệnh in đã vượt giới hạn in lại tối đa 3 lần."), null);

        var normalizedReason = NormalizeCode(request.ReasonCode);
        var reasonExists = await _masterDb.ReasonCodes
            .AnyAsync(x => x.ReasonType == "LABEL_REPRINT" && x.Code == normalizedReason && x.IsActive, cancellationToken);
        if (!reasonExists) return (OperationResult.Fail("INVALID_REASON_CODE", "Lý do in lại không hợp lệ."), null);

        source.ReprintCount += 1;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        source.UpdatedBy = username;

        var job = new PrintJob
        {
            Id = Guid.NewGuid(),
            TenantId = _db.CurrentTenantId,
            TemplateId = source.TemplateId,
            PrinterCode = source.PrinterCode,
            Status = "queued",
            IdempotencyKey = request.IdempotencyKey.Trim(),
            PayloadJson = source.PayloadJson,
            RenderedCommand = source.RenderedCommand,
            RenderedCommandHash = source.RenderedCommandHash,
            SourceJobId = source.Id,
            ReasonCode = normalizedReason,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = username
        };

        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        job.Template = source.Template;

        return (OperationResult.Ok(), ToDto(job));
    }

    private static OperationResult ValidateCreateRequest(CreatePrintJobRequest request)
    {
        if (request.TemplateId == Guid.Empty) return OperationResult.Fail("TEMPLATE_ID_REQUIRED", "Bắt buộc chọn mẫu tem.");
        if (string.IsNullOrWhiteSpace(request.PrinterCode)) return OperationResult.Fail("PRINTER_CODE_REQUIRED", "Bắt buộc chọn máy in.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) return OperationResult.Fail("IDEMPOTENCY_KEY_REQUIRED", "Bắt buộc truyền khóa chống gửi trùng.");
        if (request.Payload is null) return OperationResult.Fail("PAYLOAD_REQUIRED", "Bắt buộc truyền dữ liệu in tem.");
        return OperationResult.Ok();
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static PrintJobDto ToDto(PrintJob x) => new(
        x.Id,
        x.TemplateId,
        x.Template.TemplateCode,
        x.PrinterCode,
        x.Status,
        x.IdempotencyKey,
        x.RenderedCommandHash,
        x.SourceJobId,
        x.ReasonCode,
        x.ReprintCount,
        x.CreatedAt,
        x.ErrorMessage);
}
