using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.ErpIntegration.Contexts;
using Nexustock.Modules.ErpIntegration.Entities;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.Modules.ErpIntegration.Services;

public class ImportPreviewService : IImportPreviewService
{
    private readonly ErpIntegrationDbContext _context;
    private readonly MasterDataDbContext _masterContext;

    public ImportPreviewService(ErpIntegrationDbContext context, MasterDataDbContext masterContext)
    {
        _context = context;
        _masterContext = masterContext;
    }

    public async Task<ImportPreviewResult> PreviewMappingsAsync(Guid tenantId, string externalSystem, string csvContent)
    {
        var rawRows = CsvParser.Parse(csvContent);
        if (rawRows.Count <= 1)
        {
            return new ImportPreviewResult
            {
                JobId = Guid.Empty,
                ImportType = "mappings",
                Status = "failed",
                Message = "File CSV trống hoặc chỉ chứa tiêu đề."
            };
        }

        var header = rawRows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var dataRows = rawRows.Skip(1).ToList();
        var jobId = Guid.NewGuid();

        // Check required headers
        var requiredHeaders = new[] { "mappingtype", "externalcode", "internalcode", "status" };
        foreach (var req in requiredHeaders)
        {
            if (!header.Contains(req))
            {
                return new ImportPreviewResult
                {
                    JobId = Guid.Empty,
                    ImportType = "mappings",
                    Status = "failed",
                    Message = $"Thiếu cột bắt buộc: {req}."
                };
            }
        }

        // Cache MasterData to speed up validation
        var existingProductCodes = await _masterContext.Products.Where(p => p.TenantId == tenantId && p.IsActive).Select(p => p.Code).ToListAsync();
        var existingWarehouseCodes = await _masterContext.Warehouses.Where(w => w.TenantId == tenantId && w.IsActive).Select(w => w.Code).ToListAsync();
        var existingPartnerCodes = await _masterContext.Partners.Where(p => p.TenantId == tenantId && p.IsActive).Select(p => p.Code).ToListAsync();
        var existingUomCodes = await _masterContext.Uoms.Where(u => u.TenantId == tenantId && u.IsActive).Select(u => u.Code).ToListAsync();

        var rows = new List<ImportPreviewRowDto>();
        var validCount = 0;
        var errorCount = 0;

        for (int i = 0; i < dataRows.Count; i++)
        {
            var row = dataRows[i];
            var rowIndex = i + 1;
            
            var rawMap = new Dictionary<string, string>();
            for (int h = 0; h < header.Length; h++)
            {
                rawMap[header[h]] = h < row.Length ? row[h].Trim() : string.Empty;
            }

            var isValid = true;
            var errorMsg = new StringBuilder();

            rawMap.TryGetValue("mappingtype", out var type);
            rawMap.TryGetValue("externalcode", out var extCode);
            rawMap.TryGetValue("internalcode", out var intCode);
            rawMap.TryGetValue("status", out var status);

            type = type?.Trim().ToLowerInvariant();
            status = status?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(type) || !new[] { "item", "warehouse", "partner", "uom" }.Contains(type))
            {
                isValid = false;
                errorMsg.Append("Loại mapping phải là: item, warehouse, partner, uom. ");
            }

            if (string.IsNullOrWhiteSpace(extCode))
            {
                isValid = false;
                errorMsg.Append("Mã ERP không được để trống. ");
            }

            if (string.IsNullOrWhiteSpace(intCode))
            {
                isValid = false;
                errorMsg.Append("Mã WMS không được để trống. ");
            }
            else
            {
                // Validate internal code existence
                if (type == "item" && !existingProductCodes.Contains(intCode))
                {
                    isValid = false;
                    errorMsg.Append($"Mã vật tư WMS '{intCode}' không tồn tại hoặc bị tắt. ");
                }
                else if (type == "warehouse" && !existingWarehouseCodes.Contains(intCode))
                {
                    isValid = false;
                    errorMsg.Append($"Mã kho WMS '{intCode}' không tồn tại hoặc bị tắt. ");
                }
                else if (type == "partner" && !existingPartnerCodes.Contains(intCode))
                {
                    isValid = false;
                    errorMsg.Append($"Mã đối tác WMS '{intCode}' không tồn tại hoặc bị tắt. ");
                }
                else if (type == "uom" && !existingUomCodes.Contains(intCode))
                {
                    isValid = false;
                    errorMsg.Append($"Mã UOM WMS '{intCode}' không tồn tại hoặc bị tắt. ");
                }
            }

            if (string.IsNullOrWhiteSpace(status) || !new[] { "active", "inactive" }.Contains(status))
            {
                isValid = false;
                errorMsg.Append("Trạng thái phải là active hoặc inactive. ");
            }

            if (isValid) validCount++;
            else errorCount++;

            rows.Add(new ImportPreviewRowDto
            {
                RowIndex = rowIndex,
                RawData = rawMap,
                IsValid = isValid,
                ErrorMessage = isValid ? null : errorMsg.ToString().Trim()
            });
        }

        var result = new ImportPreviewResult
        {
            JobId = jobId,
            ImportType = "mappings",
            Status = errorCount > 0 ? "failed_validation" : "previewed",
            TotalRows = dataRows.Count,
            ValidRows = validCount,
            ErrorRows = errorCount,
            Rows = rows
        };

        // Save preview job state to DB
        var payload = JsonSerializer.Serialize(result);
        var job = new IntegrationImportJob
        {
            Id = jobId,
            TenantId = tenantId,
            ImportType = "mappings",
            FileName = "import_mappings.csv",
            Status = result.Status,
            TotalRows = result.TotalRows,
            ValidRows = result.ValidRows,
            ErrorRows = result.ErrorRows,
            PreviewPayload = payload,
            TraceId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        _context.IntegrationImportJobs.Add(job);
        await _context.SaveChangesAsync();

        return result;
    }

    private static class CsvParser
    {
        public static List<string[]> Parse(string csvContent)
        {
            var result = new List<string[]>();
            if (string.IsNullOrEmpty(csvContent)) return result;

            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var fields = ParseCsvLine(line);
                result.Add(fields);
            }
            return result;
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var inQuotes = false;
            var currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            fields.Add(currentField.ToString());
            return fields.ToArray();
        }
    }
}
