using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Entities;

namespace Nexustock.Modules.MasterData.Services;

public enum BatchClaimStatus
{
    Success,
    NotFound,
    AlreadyCommitted,
    Expired,
    HasErrors,
    TargetMismatch,
    TypeMismatch,
    OwnerMismatch,
    InvalidStatus
}

public sealed record BatchClaimResult(
    BatchClaimStatus Status,
    ImportBatch? Batch = null,
    string? ErrorMessage = null
);

public interface IImportBatchCoordinator
{
    string ComputeHash(byte[] contentBytes);
    string ComputeHash(string textContent);
    Task<ImportBatch?> FindDuplicatePreviewAsync(Guid tenantId, string importType, Guid? targetId, string fileHash, string createdBy, CancellationToken cancellationToken);
    Task<ImportBatch> CreateBatchAsync(Guid tenantId, string importType, Guid? targetId, string? fileName, string fileHash, string createdBy, int totalRows, int successRows, int errorRows, CancellationToken cancellationToken);
    Task<BatchClaimResult> ClaimBatchForCommitAsync(Guid batchId, Guid tenantId, string expectedImportType, Guid? expectedTargetId, string username, CancellationToken cancellationToken);
    Task MarkCommittedAsync(Guid batchId, string username, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid batchId, string errorMessage, CancellationToken cancellationToken);
}

public class ImportBatchCoordinator : IImportBatchCoordinator
{
    private readonly MasterDataDbContext _db;
    public static readonly TimeSpan BatchTtl = TimeSpan.FromHours(24);

    public ImportBatchCoordinator(MasterDataDbContext db)
    {
        _db = db;
    }

    public string ComputeHash(byte[] contentBytes)
    {
        var hash = SHA256.HashData(contentBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string ComputeHash(string textContent)
    {
        return ComputeHash(Encoding.UTF8.GetBytes(textContent));
    }

    public async Task<ImportBatch?> FindDuplicatePreviewAsync(
        Guid tenantId,
        string importType,
        Guid? targetId,
        string fileHash,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var type = importType.Trim().ToUpperInvariant();

        return await _db.ImportBatches
            .Where(b => b.TenantId == tenantId &&
                        b.ImportType == type &&
                        b.TargetId == targetId &&
                        b.FileHash == fileHash &&
                        b.CreatedBy == createdBy &&
                        (b.Status == "PREVIEWED" || b.Status == "VALIDATED") &&
                        (b.ExpiresAt == null || b.ExpiresAt > now))
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ImportBatch> CreateBatchAsync(
        Guid tenantId,
        string importType,
        Guid? targetId,
        string? fileName,
        string fileHash,
        string createdBy,
        int totalRows,
        int successRows,
        int errorRows,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ImportType = importType.Trim().ToUpperInvariant(),
            TargetId = targetId,
            FileName = fileName,
            FileHash = fileHash,
            Status = "PREVIEWED",
            TotalRows = totalRows,
            SuccessRows = successRows,
            ErrorRows = errorRows,
            CreatedAt = now,
            CreatedBy = createdBy,
            ExpiresAt = now.Add(BatchTtl),
            RowVersion = 1
        };

        await _db.ImportBatches.AddAsync(batch, cancellationToken);
        return batch;
    }

    public async Task<BatchClaimResult> ClaimBatchForCommitAsync(
        Guid batchId,
        Guid tenantId,
        string expectedImportType,
        Guid? expectedTargetId,
        string username,
        CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId, cancellationToken);

        if (batch is null)
        {
            return new BatchClaimResult(BatchClaimStatus.NotFound, null, "IMPORT_BATCH_NOT_FOUND");
        }

        if (batch.Status == "COMMITTED")
        {
            return new BatchClaimResult(BatchClaimStatus.AlreadyCommitted, batch, "IMPORT_BATCH_ALREADY_COMMITTED");
        }

        if (batch.ExpiresAt.HasValue && batch.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            batch.Status = "EXPIRED";
            await _db.SaveChangesAsync(cancellationToken);
            return new BatchClaimResult(BatchClaimStatus.Expired, batch, "IMPORT_BATCH_EXPIRED");
        }

        if (!string.Equals(batch.ImportType, expectedImportType, StringComparison.OrdinalIgnoreCase))
        {
            return new BatchClaimResult(BatchClaimStatus.TypeMismatch, batch, "IMPORT_TYPE_MISMATCH");
        }

        if (!string.Equals(batch.CreatedBy, username, StringComparison.OrdinalIgnoreCase))
        {
            return new BatchClaimResult(BatchClaimStatus.OwnerMismatch, batch, "IMPORT_BATCH_NOT_FOUND");
        }

        if (batch.TargetId != expectedTargetId)
        {
            return new BatchClaimResult(BatchClaimStatus.TargetMismatch, batch, "IMPORT_TARGET_MISMATCH");
        }

        if (batch.ErrorRows > 0)
        {
            return new BatchClaimResult(BatchClaimStatus.HasErrors, batch, "IMPORT_BATCH_HAS_ERRORS");
        }

        if (batch.Status != "PREVIEWED" && batch.Status != "VALIDATED")
        {
            return new BatchClaimResult(BatchClaimStatus.InvalidStatus, batch, $"IMPORT_BATCH_INVALID_STATUS_{batch.Status}");
        }

        // Atomic claim transition: PREVIEWED/VALIDATED -> COMMITTING
        batch.Status = "COMMITTING";
        batch.RowVersion++;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new BatchClaimResult(BatchClaimStatus.AlreadyCommitted, batch, "IMPORT_BATCH_ALREADY_COMMITTED");
        }

        return new BatchClaimResult(BatchClaimStatus.Success, batch);
    }

    public async Task MarkCommittedAsync(Guid batchId, string username, CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch != null)
        {
            batch.Status = "COMMITTED";
            batch.CommittedBy = username;
            batch.CommittedAt = DateTimeOffset.UtcNow;
            batch.RowVersion++;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkFailedAsync(Guid batchId, string errorMessage, CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch != null)
        {
            batch.Status = "FAILED";
            batch.RowVersion++;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
