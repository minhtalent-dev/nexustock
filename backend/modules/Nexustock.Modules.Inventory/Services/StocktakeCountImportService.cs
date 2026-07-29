using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Entities;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.Inventory.Services;

public interface IStocktakeCountImportService
{
    Task<ImportResultDto> PreviewImportAsync(Guid stocktakeId, string contentType, Stream stream, string fileName, string username, CancellationToken cancellationToken);
    Task<ImportResultDto> CommitImportAsync(Guid stocktakeId, Guid batchId, string username, CancellationToken cancellationToken);
    Task<string?> ExportErrorCsvAsync(Guid stocktakeId, Guid batchId, string username, CancellationToken cancellationToken);
}

public class StocktakeCountImportService : IStocktakeCountImportService
{
    private readonly InventoryDbContext _inventoryDb;
    private readonly MasterDataDbContext _masterDb;
    private readonly IImportBatchCoordinator _batchCoordinator;

    public StocktakeCountImportService(
        InventoryDbContext inventoryDb,
        MasterDataDbContext masterDb,
        IImportBatchCoordinator batchCoordinator)
    {
        _inventoryDb = inventoryDb;
        _masterDb = masterDb;
        _batchCoordinator = batchCoordinator;
    }

    public async Task<ImportResultDto> PreviewImportAsync(
        Guid stocktakeId,
        string contentType,
        Stream stream,
        string fileName,
        string username,
        CancellationToken cancellationToken)
    {
        var tenantId = _inventoryDb.CurrentTenantId;
        var stocktake = await _inventoryDb.Stocktakes
            .FirstOrDefaultAsync(s => s.Id == stocktakeId && s.TenantId == tenantId, cancellationToken);

        if (stocktake == null)
        {
            return new ImportResultDto(false, Guid.Empty, "STOCKTAKE_COUNTS", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "Không tìm thấy đợt kiểm kê.", stocktakeId);
        }

        if (stocktake.Status != "Counting")
        {
            return new ImportResultDto(false, Guid.Empty, "STOCKTAKE_COUNTS", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), $"Đợt kiểm kê ở trạng thái '{stocktake.Status}' không ở trạng thái Counting.", stocktakeId);
        }

        await using var content = new MemoryStream();
        await stream.CopyToAsync(content, cancellationToken);
        var fileBytes = content.ToArray();
        content.Position = 0;

        List<string[]> rawRows;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".xlsx")
        {
            rawRows = SpreadsheetReader.ReadSheetRows(content).ToList();
        }
        else
        {
            using var reader = new StreamReader(content, Encoding.UTF8, leaveOpen: true);
            var csvContent = await reader.ReadToEndAsync(cancellationToken);
            rawRows = CsvParser.Parse(csvContent);
        }

        if (rawRows.Count <= 1)
        {
            return new ImportResultDto(false, Guid.Empty, "STOCKTAKE_COUNTS", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "File trống hoặc chỉ có header.", stocktakeId);
        }

        var dataRows = rawRows.Skip(1).ToList();
        if (dataRows.Count > SpreadsheetReader.MaxDataRows)
        {
            return new ImportResultDto(false, Guid.Empty, "STOCKTAKE_COUNTS", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "IMPORT_TOO_LARGE", stocktakeId);
        }

        var header = rawRows[0].Select(h => h.Trim()).ToArray();
        var requiredHeaders = new[] { "lineNo", "locationCode", "sku", "lotNo", "countQty", "uomCode" };
        if (requiredHeaders.Any(required => !header.Contains(required, StringComparer.OrdinalIgnoreCase)))
        {
            return new ImportResultDto(false, Guid.Empty, "STOCKTAKE_COUNTS", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "IMPORT_TEMPLATE_VERSION_UNSUPPORTED", stocktakeId);
        }

        var fileHash = _batchCoordinator.ComputeHash(fileBytes);

        // Duplicate preview check
        var existingPreview = await _batchCoordinator.FindDuplicatePreviewAsync(tenantId, "STOCKTAKE_COUNTS", stocktakeId, fileHash, username, cancellationToken);
        if (existingPreview != null)
        {
            var prevRows = await _masterDb.ImportBatchRows.Where(r => r.BatchId == existingPreview.Id).ToListAsync(cancellationToken);
            var prevErrors = prevRows.Where(r => !r.IsValid).Select(r => new ImportRowErrorDto(
                r.RowIndex,
                JsonSerializer.Deserialize<Dictionary<string, string>>(r.RawData!)!,
                r.ErrorMessage ?? ""
            )).ToList();

            return new ImportResultDto(
                existingPreview.ErrorRows == 0,
                existingPreview.Id,
                "STOCKTAKE_COUNTS",
                existingPreview.Status,
                existingPreview.TotalRows,
                existingPreview.SuccessRows,
                existingPreview.ErrorRows,
                prevErrors,
                null,
                stocktakeId,
                existingPreview.ExpiresAt
            );
        }

        // Bulk load master data
        var locations = await _masterDb.StorageLocations
            .Where(l => l.TenantId == tenantId)
            .ToDictionaryAsync(l => l.Code.ToUpperInvariant(), l => l, cancellationToken);

        var products = await _masterDb.Products
            .Include(p => p.BaseUom)
            .Where(p => p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.Code.ToUpperInvariant(), p => p, cancellationToken);

        var errors = new List<ImportRowErrorDto>();
        var batchRows = new List<ImportBatchRow>();
        var seenKeys = new HashSet<(string LocationCode, string Sku, string LotNo)>();

        var batch = await _batchCoordinator.CreateBatchAsync(
            tenantId, "STOCKTAKE_COUNTS", stocktakeId, fileName, fileHash, username, dataRows.Count, 0, 0, cancellationToken);

        for (int i = 0; i < dataRows.Count; i++)
        {
            var row = dataRows[i];
            var rowIndex = i + 1;
            var rawMap = new Dictionary<string, string>();
            for (int h = 0; h < header.Length; h++)
            {
                rawMap[header[h]] = h < row.Length ? row[h] : string.Empty;
            }

            var isValid = true;
            var errorMsg = new StringBuilder();

            var locationCode = GetVal(row, header, "locationCode")?.ToUpperInvariant();
            var sku = GetVal(row, header, "sku")?.ToUpperInvariant();
            var lotNo = GetVal(row, header, "lotNo")?.Trim() ?? "";
            var qtyStr = GetVal(row, header, "countQty");
            var uomCode = GetVal(row, header, "uomCode")?.ToUpperInvariant();

            StorageLocation? loc = null;
            if (string.IsNullOrWhiteSpace(locationCode) || !locations.TryGetValue(locationCode, out loc))
            {
                isValid = false;
                errorMsg.Append($"Vị trí '{locationCode}' không tồn tại trong hệ thống. ");
            }
            else if (stocktake.ZoneId.HasValue && loc.ZoneId != stocktake.ZoneId.Value)
            {
                isValid = false;
                errorMsg.Append($"Vị trí '{locationCode}' không thuộc Zone của đợt kiểm kê. ");
            }

            Product? prod = null;
            if (string.IsNullOrWhiteSpace(sku) || !products.TryGetValue(sku, out prod))
            {
                isValid = false;
                errorMsg.Append($"Mã sản phẩm '{sku}' không tồn tại trong hệ thống. ");
            }
            else if (!prod.IsActive)
            {
                isValid = false;
                errorMsg.Append($"Sản phẩm '{sku}' đang bị khóa (không hoạt động). ");
            }

            if (prod != null)
            {
                var baseUomCode = prod.BaseUom?.Code.ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(uomCode) || baseUomCode == null || !string.Equals(uomCode, baseUomCode, StringComparison.OrdinalIgnoreCase))
                {
                    isValid = false;
                    errorMsg.Append($"Đơn vị tính '{uomCode}' không khớp Base UOM '{baseUomCode}' của sản phẩm '{sku}'. ");
                }
            }

            if (!string.IsNullOrWhiteSpace(locationCode) && !string.IsNullOrWhiteSpace(sku))
            {
                var naturalKey = (locationCode, sku, lotNo);
                if (seenKeys.Contains(naturalKey))
                {
                    isValid = false;
                    errorMsg.Append($"Dòng kiểm đếm cho vị trí '{locationCode}', SKU '{sku}', Lô '{lotNo}' bị trùng trong file. ");
                }
                else
                {
                    seenKeys.Add(naturalKey);
                }
            }

            if (string.IsNullOrWhiteSpace(qtyStr) || !decimal.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var countQty) || countQty < 0)
            {
                isValid = false;
                errorMsg.Append("Số lượng kiểm đếm phải là số không âm. ");
            }

            var finalError = errorMsg.ToString().Trim();
            if (!isValid)
            {
                errors.Add(new ImportRowErrorDto(rowIndex, rawMap, finalError));
            }

            batchRows.Add(new ImportBatchRow
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                RowIndex = rowIndex,
                RawData = JsonSerializer.Serialize(rawMap),
                IsValid = isValid,
                ErrorMessage = isValid ? null : finalError
            });
        }

        batch.SuccessRows = batchRows.Count(x => x.IsValid);
        batch.ErrorRows = batchRows.Count(x => !x.IsValid);

        await _masterDb.ImportBatchRows.AddRangeAsync(batchRows, cancellationToken);
        await _masterDb.SaveChangesAsync(cancellationToken);

        return new ImportResultDto(
            errors.Count == 0,
            batch.Id,
            "STOCKTAKE_COUNTS",
            batch.Status,
            batch.TotalRows,
            batch.SuccessRows,
            batch.ErrorRows,
            errors,
            null,
            stocktakeId,
            batch.ExpiresAt
        );
    }

    public async Task<ImportResultDto> CommitImportAsync(Guid stocktakeId, Guid batchId, string username, CancellationToken cancellationToken)
    {
        var tenantId = _inventoryDb.CurrentTenantId;
        var claimResult = await _batchCoordinator.ClaimBatchForCommitAsync(
            batchId, tenantId, "STOCKTAKE_COUNTS", stocktakeId, username, cancellationToken);

        if (claimResult.Status != BatchClaimStatus.Success)
        {
            return new ImportResultDto(
                false, batchId, "STOCKTAKE_COUNTS", claimResult.Batch?.Status ?? "FAILED",
                claimResult.Batch?.TotalRows ?? 0, claimResult.Batch?.SuccessRows ?? 0, claimResult.Batch?.ErrorRows ?? 0,
                new List<ImportRowErrorDto>(), claimResult.ErrorMessage ?? "Không thể duyệt batch import.", stocktakeId);
        }

        var stocktake = await _inventoryDb.Stocktakes
            .FirstOrDefaultAsync(s => s.Id == stocktakeId && s.TenantId == tenantId, cancellationToken);

        if (stocktake == null || stocktake.Status != "Counting")
        {
            await _batchCoordinator.MarkFailedAsync(batchId, "IMPORT_TARGET_STATE_INVALID", cancellationToken);
            return new ImportResultDto(false, batchId, "STOCKTAKE_COUNTS", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "IMPORT_TARGET_STATE_INVALID", stocktakeId);
        }

        var rows = await _masterDb.ImportBatchRows
            .Where(r => r.BatchId == batchId && r.IsValid)
            .OrderBy(r => r.RowIndex)
            .ToListAsync(cancellationToken);

        var locations = await _masterDb.StorageLocations.ToDictionaryAsync(l => l.Code.ToUpperInvariant(), l => l.Id, cancellationToken);
        var products = await _masterDb.Products.ToDictionaryAsync(p => p.Code.ToUpperInvariant(), p => p.Id, cancellationToken);

        var existingStocktakeItems = await _inventoryDb.StocktakeItems
            .Where(si => si.StocktakeId == stocktakeId && si.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var existingInventories = await _inventoryDb.Inventories
            .Where(inv => inv.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        IDbContextTransaction? tx = null;
        if (_masterDb.Database.IsRelational())
        {
            tx = await _masterDb.Database.BeginTransactionAsync(cancellationToken);
            if (_inventoryDb.Database.IsRelational())
            {
                await _inventoryDb.Database.UseTransactionAsync(tx.GetDbTransaction(), cancellationToken);
            }
        }

        try
        {
            foreach (var r in rows)
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(r.RawData!)!;
                var locationCode = map["locationCode"].Trim().ToUpperInvariant();
                var sku = map["sku"].Trim().ToUpperInvariant();
                var lotNo = map.TryGetValue("lotNo", out var l) ? l.Trim() : "";
                var countQty = decimal.Parse(map["countQty"], System.Globalization.CultureInfo.InvariantCulture);

                var locationId = locations[locationCode];
                var productId = products[sku];

                var existingItem = existingStocktakeItems.FirstOrDefault(si =>
                    si.LocationId == locationId && si.ItemId == productId && si.LotNo == lotNo);

                if (existingItem != null)
                {
                    existingItem.CountedQty = countQty;
                    existingItem.VarianceQty = countQty - existingItem.SystemQty;
                    existingItem.Status = "Counted";
                    existingItem.UpdatedAt = DateTime.UtcNow;
                    existingItem.UpdatedBy = username;
                }
                else
                {
                    var invBalance = existingInventories.FirstOrDefault(inv =>
                        inv.LocationId == locationId && inv.ItemId == productId && inv.LotNo == lotNo);
                    var systemQty = invBalance?.QtyOnHand ?? 0.0000m;

                    var newItem = new StocktakeItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        StocktakeId = stocktakeId,
                        LocationId = locationId,
                        ItemId = productId,
                        LotNo = lotNo,
                        SystemQty = systemQty,
                        CountedQty = countQty,
                        VarianceQty = countQty - systemQty,
                        Status = "Counted",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username
                    };
                    _inventoryDb.StocktakeItems.Add(newItem);
                    existingStocktakeItems.Add(newItem);
                }
            }

            stocktake.UpdatedAt = DateTime.UtcNow;
            stocktake.UpdatedBy = username;

            await _inventoryDb.SaveChangesAsync(cancellationToken);

            await _batchCoordinator.MarkCommittedAsync(batchId, username, cancellationToken);
            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return new ImportResultDto(true, batchId, "STOCKTAKE_COUNTS", "COMMITTED", claimResult.Batch!.TotalRows, claimResult.Batch.SuccessRows, 0, new List<ImportRowErrorDto>(), null, stocktakeId);
        }
        catch (Exception ex)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            _inventoryDb.ChangeTracker.Clear();
            _masterDb.ChangeTracker.Clear();
            await _batchCoordinator.MarkFailedAsync(batchId, ex.Message, cancellationToken);
            return new ImportResultDto(false, batchId, "STOCKTAKE_COUNTS", "FAILED", claimResult.Batch!.TotalRows, 0, claimResult.Batch.ErrorRows, new List<ImportRowErrorDto>(), $"Commit thất bại: {ex.Message}", stocktakeId);
        }
    }

    public async Task<string?> ExportErrorCsvAsync(Guid stocktakeId, Guid batchId, string username, CancellationToken cancellationToken)
    {
        var batch = await _masterDb.ImportBatches.FirstOrDefaultAsync(b =>
            b.Id == batchId && b.ImportType == "STOCKTAKE_COUNTS" && b.TargetId == stocktakeId && b.CreatedBy == username,
            cancellationToken);
        if (batch is null) return null;

        var rows = await _masterDb.ImportBatchRows
            .Where(r => r.BatchId == batchId && !r.IsValid)
            .OrderBy(r => r.RowIndex)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return null;

        var firstRow = JsonSerializer.Deserialize<Dictionary<string, string>>(rows[0].RawData!)!;
        var header = firstRow.Keys.ToList();

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine(string.Join(",", header.Select(SpreadsheetReader.SanitizeFormula)) + ",errorMessage");

        foreach (var r in rows)
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(r.RawData!)!;
            var fields = header.Select(h => map.TryGetValue(h, out var v) ? SpreadsheetReader.SanitizeFormula(v) : "").ToList();
            fields.Add(SpreadsheetReader.SanitizeFormula(r.ErrorMessage ?? ""));
            csvBuilder.AppendLine(string.Join(",", fields.Select(SpreadsheetReader.EscapeCsv)));
        }

        return csvBuilder.ToString();
    }

    private static string? GetVal(string[] row, string[] header, string key)
    {
        var idx = Array.FindIndex(header, h => h.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && idx < row.Length) return row[idx].Trim();
        return null;
    }
}
