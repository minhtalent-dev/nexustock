using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.ErpIntegration.Contexts;
using Nexustock.Modules.ErpIntegration.Entities;

namespace Nexustock.Modules.ErpIntegration.Services;

public class ImportCommitService : IImportCommitService
{
    private readonly ErpIntegrationDbContext _context;

    public ImportCommitService(ErpIntegrationDbContext context)
    {
        _context = context;
    }

    public async Task<ImportPreviewResult> CommitImportAsync(Guid tenantId, Guid jobId)
    {
        var job = await _context.IntegrationImportJobs
            .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == jobId);

        if (job == null)
        {
            return new ImportPreviewResult
            {
                JobId = jobId,
                Status = "failed",
                Message = "Không tìm thấy phiên import."
            };
        }

        if (job.ExpiresAt < DateTimeOffset.UtcNow)
        {
            job.Status = "expired";
            await _context.SaveChangesAsync();
            return new ImportPreviewResult
            {
                JobId = jobId,
                Status = "expired",
                Message = "Phiên import đã hết hạn (quá 30 phút)."
            };
        }

        if (job.Status == "committed")
        {
            return new ImportPreviewResult
            {
                JobId = jobId,
                Status = "committed",
                Message = "Phiên import đã được commit trước đó."
            };
        }

        if (job.ErrorRows > 0)
        {
            return new ImportPreviewResult
            {
                JobId = jobId,
                Status = "failed",
                Message = "Không thể commit phiên import có chứa dòng lỗi."
            };
        }

        var preview = JsonSerializer.Deserialize<ImportPreviewResult>(job.PreviewPayload);
        if (preview == null)
        {
            return new ImportPreviewResult
            {
                JobId = jobId,
                Status = "failed",
                Message = "Dữ liệu preview bị lỗi định dạng hoặc trống."
            };
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var row in preview.Rows)
            {
                row.RawData.TryGetValue("mappingtype", out var type);
                row.RawData.TryGetValue("externalcode", out var extCode);
                row.RawData.TryGetValue("internalcode", out var intCode);
                row.RawData.TryGetValue("status", out var status);

                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(extCode) || string.IsNullOrEmpty(intCode))
                {
                    throw new Exception("Dữ liệu hàng bị thiếu thông tin bắt buộc khi commit.");
                }

                // Upsert logic for IntegrationMappings
                var mapping = await _context.IntegrationMappings
                    .FirstOrDefaultAsync(m => m.TenantId == tenantId && 
                                              m.ExternalSystem == "SAP-ERP" && 
                                              m.MappingType == type.ToLowerInvariant() && 
                                              m.ExternalCode == extCode);

                if (mapping == null)
                {
                    mapping = new IntegrationMapping
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ExternalSystem = "SAP-ERP",
                        MappingType = type.ToLowerInvariant(),
                        ExternalCode = extCode,
                        InternalCode = intCode,
                        Status = status?.ToLowerInvariant() == "inactive" ? "inactive" : "active",
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _context.IntegrationMappings.Add(mapping);
                }
                else
                {
                    mapping.InternalCode = intCode;
                    mapping.Status = status?.ToLowerInvariant() == "inactive" ? "inactive" : "active";
                    mapping.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            job.Status = "committed";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            preview.Status = "committed";
            preview.Message = "Nhập dữ liệu ánh xạ thành công.";
            return preview;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            job.Status = "failed";
            await _context.SaveChangesAsync();

            return new ImportPreviewResult
            {
                JobId = jobId,
                Status = "failed",
                Message = $"Lỗi hệ thống khi commit giao dịch: {ex.Message}"
            };
        }
    }
}
