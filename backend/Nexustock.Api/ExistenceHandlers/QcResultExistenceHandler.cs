using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Qc.Contexts;

namespace Nexustock.Api.ExistenceHandlers;

public class QcResultExistenceHandler : IEntityExistenceHandler
{
    private readonly QcDbContext _dbContext;

    public QcResultExistenceHandler(QcDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool CanHandle(string entityType) => entityType.Equals("QC_RESULT", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> ExistsAsync(Guid entityId, CancellationToken ct)
    {
        return await _dbContext.QcResults.AnyAsync(q => q.Id == entityId, ct);
    }
}
