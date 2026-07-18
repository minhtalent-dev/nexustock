using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nexustock.Modules.ErpIntegration.Services;

public class ImportPreviewRowDto
{
    public int RowIndex { get; set; }
    public Dictionary<string, string> RawData { get; set; } = new();
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ImportPreviewResult
{
    public Guid JobId { get; set; }
    public string ImportType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public List<ImportPreviewRowDto> Rows { get; set; } = new();
    public string? Message { get; set; }
}

public interface IImportPreviewService
{
    Task<ImportPreviewResult> PreviewMappingsAsync(Guid tenantId, string externalSystem, string csvContent);
}
