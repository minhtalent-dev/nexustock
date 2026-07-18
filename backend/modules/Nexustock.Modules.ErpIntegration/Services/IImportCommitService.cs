using System;
using System.Threading.Tasks;

namespace Nexustock.Modules.ErpIntegration.Services;

public interface IImportCommitService
{
    Task<ImportPreviewResult> CommitImportAsync(Guid tenantId, Guid jobId);
}
