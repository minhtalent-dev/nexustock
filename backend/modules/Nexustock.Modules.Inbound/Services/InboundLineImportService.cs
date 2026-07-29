using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Entities;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.Inbound.Services;

public interface IInboundLineImportService
{
    Task<ImportResultDto> PreviewImportAsync(Guid orderId, string contentType, Stream stream, string fileName, string username, CancellationToken cancellationToken);
    Task<ImportResultDto> CommitImportAsync(Guid orderId, Guid batchId, string username, CancellationToken cancellationToken);
    Task<string?> ExportErrorCsvAsync(Guid orderId, Guid batchId, string username, CancellationToken cancellationToken);
}

public class InboundLineImportService : IInboundLineImportService
{
    private readonly InboundDbContext _inboundDb;
    private readonly MasterDataDbContext _masterDb;
    private readonly IImportBatchCoordinator _batchCoordinator;

    public InboundLineImportService(
        InboundDbContext inboundDb,
        MasterDataDbContext masterDb,
        IImportBatchCoordinator batchCoordinator)
    {
        _inboundDb = inboundDb;
        _masterDb = masterDb;
        _batchCoordinator = batchCoordinator;
    }

    public async Task<ImportResultDto> PreviewImportAsync(
        Guid orderId,
        string contentType,
        Stream stream,
        string fileName,
        string username,
        CancellationToken cancellationToken)
    {
        var tenantId = _inboundDb.CurrentTenantId;
        var order = await _inboundDb.InboundOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order == null)
        {
            return new ImportResultDto(false, Guid.Empty, "INBOUND_LINES", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "Không tìm thấy đơn nhập hàng.", orderId);
        }

        if (order.Status != InboundOrderStatus.Draft && order.Status != InboundOrderStatus.Open)
        {
            return new ImportResultDto(false, Guid.Empty, "INBOUND_LINES", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), $"Đơn nhập hàng ở trạng thái '{order.Status}' không cho phép nhập dòng.", orderId);
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
            return new ImportResultDto(false, Guid.Empty, "INBOUND_LINES", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "File trống hoặc chỉ có header.", orderId);
        }

        var dataRows = rawRows.Skip(1).ToList();
        if (dataRows.Count > SpreadsheetReader.MaxDataRows)
        {
            return new ImportResultDto(false, Guid.Empty, "INBOUND_LINES", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "IMPORT_TOO_LARGE", orderId);
        }

        var header = rawRows[0].Select(h => h.Trim()).ToArray();
        var requiredHeaders = new[] { "sku", "uomCode", "expectedQty", "tolerance" };
        if (requiredHeaders.Any(required => !header.Contains(required, StringComparer.OrdinalIgnoreCase)))
        {
            return new ImportResultDto(false, Guid.Empty, "INBOUND_LINES", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "IMPORT_TEMPLATE_VERSION_UNSUPPORTED", orderId);
        }

        var fileHash = _batchCoordinator.ComputeHash(fileBytes);

        // Check duplicate preview
        var existingPreview = await _batchCoordinator.FindDuplicatePreviewAsync(tenantId, "INBOUND_LINES", orderId, fileHash, username, cancellationToken);
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
                "INBOUND_LINES",
                existingPreview.Status,
                existingPreview.TotalRows,
                existingPreview.SuccessRows,
                existingPreview.ErrorRows,
                prevErrors,
                null,
                orderId,
                existingPreview.ExpiresAt
            );
        }

        // Bulk load master data for validation
        var products = await _masterDb.Products
            .Where(p => p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.Code.ToUpperInvariant(), p => p, cancellationToken);

        var uoms = await _masterDb.Uoms
            .Where(u => u.TenantId == tenantId)
            .ToDictionaryAsync(u => u.Code.ToUpperInvariant(), u => u, cancellationToken);

        var packages = await _masterDb.Packages
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var existingReceivedItems = order.Items
            .Where(i => i.ReceivedQty > 0)
            .Select(i => (i.ItemId, i.UomId))
            .ToHashSet();

        var errors = new List<ImportRowErrorDto>();
        var batchRows = new List<ImportBatchRow>();
        var seenCombos = new HashSet<(Guid ItemId, Guid UomId)>();

        var batch = await _batchCoordinator.CreateBatchAsync(
            tenantId, "INBOUND_LINES", orderId, fileName, fileHash, username, dataRows.Count, 0, 0, cancellationToken);

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

            var sku = GetVal(row, header, "sku")?.ToUpperInvariant();
            var uomCode = GetVal(row, header, "uomCode")?.ToUpperInvariant();
            var qtyStr = GetVal(row, header, "expectedQty");
            var tolStr = GetVal(row, header, "tolerance");

            Product? product = null;
            if (string.IsNullOrWhiteSpace(sku) || !products.TryGetValue(sku, out product))
            {
                isValid = false;
                errorMsg.Append($"Mã SKU/Vật tư '{sku}' không tồn tại trong hệ thống. ");
            }
            else if (!product.IsActive)
            {
                isValid = false;
                errorMsg.Append($"Sản phẩm '{sku}' đang bị khóa (không hoạt động). ");
            }

            Uom? uom = null;
            if (string.IsNullOrWhiteSpace(uomCode) || !uoms.TryGetValue(uomCode, out uom))
            {
                isValid = false;
                errorMsg.Append($"Đơn vị tính '{uomCode}' không tồn tại trong hệ thống. ");
            }
            else if (!uom.IsActive)
            {
                isValid = false;
                errorMsg.Append($"Đơn vị tính '{uomCode}' đang bị khóa (không hoạt động). ");
            }

            if (product != null && uom != null)
            {
                // Check if UOM is BaseUOM or valid Package UOM for Product
                var isValidUom = product.BaseUomId == uom.Id || packages.Any(pkg => pkg.ProductId == product.Id && pkg.UomId == uom.Id);
                if (!isValidUom)
                {
                    isValid = false;
                    errorMsg.Append($"Đơn vị tính '{uomCode}' không thuộc danh mục quy đổi của sản phẩm '{sku}'. ");
                }

                var combo = (product.Id, uom.Id);
                if (seenCombos.Contains(combo))
                {
                    isValid = false;
                    errorMsg.Append($"Cặp sản phẩm '{sku}' và ĐVT '{uomCode}' bị trùng lặp trong file. ");
                }
                else
                {
                    seenCombos.Add(combo);
                }

                if (existingReceivedItems.Contains(combo))
                {
                    isValid = false;
                    errorMsg.Append($"Dòng sản phẩm '{sku}' ({uomCode}) đã có số lượng thực nhận > 0, không cho phép cập nhật từ file. ");
                }
            }

            if (string.IsNullOrWhiteSpace(qtyStr) || !decimal.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var expectedQty) || expectedQty <= 0)
            {
                isValid = false;
                errorMsg.Append("Số lượng dự kiến phải là số lớn hơn 0. ");
            }

            var tolerance = 0.0000m;
            if (!string.IsNullOrWhiteSpace(tolStr))
            {
                if (!decimal.TryParse(tolStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out tolerance) || tolerance < 0)
                {
                    isValid = false;
                    errorMsg.Append("Dung sai phải là số không âm. ");
                }
                else if (tolerance > 1.0m)
                {
                    // Convert percentage input e.g. 5 -> 0.05
                    tolerance /= 100.0m;
                }
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
            "INBOUND_LINES",
            batch.Status,
            batch.TotalRows,
            batch.SuccessRows,
            batch.ErrorRows,
            errors,
            null,
            orderId,
            batch.ExpiresAt
        );
    }

    public async Task<ImportResultDto> CommitImportAsync(Guid orderId, Guid batchId, string username, CancellationToken cancellationToken)
    {
        var tenantId = _inboundDb.CurrentTenantId;
        var claimResult = await _batchCoordinator.ClaimBatchForCommitAsync(
            batchId, tenantId, "INBOUND_LINES", orderId, username, cancellationToken);

        if (claimResult.Status != BatchClaimStatus.Success)
        {
            return new ImportResultDto(
                false, batchId, "INBOUND_LINES", claimResult.Batch?.Status ?? "FAILED",
                claimResult.Batch?.TotalRows ?? 0, claimResult.Batch?.SuccessRows ?? 0, claimResult.Batch?.ErrorRows ?? 0,
                new List<ImportRowErrorDto>(), claimResult.ErrorMessage ?? "Không thể duyệt batch import.", orderId);
        }

        var order = await _inboundDb.InboundOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order == null || (order.Status != InboundOrderStatus.Draft && order.Status != InboundOrderStatus.Open))
        {
            await _batchCoordinator.MarkFailedAsync(batchId, "IMPORT_TARGET_STATE_INVALID", cancellationToken);
            return new ImportResultDto(false, batchId, "INBOUND_LINES", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "IMPORT_TARGET_STATE_INVALID", orderId);
        }

        var rows = await _masterDb.ImportBatchRows
            .Where(r => r.BatchId == batchId && r.IsValid)
            .OrderBy(r => r.RowIndex)
            .ToListAsync(cancellationToken);

        var products = await _masterDb.Products.ToDictionaryAsync(p => p.Code.ToUpperInvariant(), p => p.Id, cancellationToken);
        var uoms = await _masterDb.Uoms.ToDictionaryAsync(u => u.Code.ToUpperInvariant(), u => u.Id, cancellationToken);

        IDbContextTransaction? tx = null;
        if (_masterDb.Database.IsRelational())
        {
            tx = await _masterDb.Database.BeginTransactionAsync(cancellationToken);
            if (_inboundDb.Database.IsRelational())
            {
                await _inboundDb.Database.UseTransactionAsync(tx.GetDbTransaction(), cancellationToken);
            }
        }

        try
        {
            foreach (var r in rows)
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(r.RawData!)!;
                var sku = map["sku"].Trim().ToUpperInvariant();
                var uomCode = map["uomCode"].Trim().ToUpperInvariant();
                var expectedQty = decimal.Parse(map["expectedQty"], System.Globalization.CultureInfo.InvariantCulture);
                var tolerance = 0.0000m;
                if (map.TryGetValue("tolerance", out var ts) && !string.IsNullOrWhiteSpace(ts) && decimal.TryParse(ts, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tol))
                {
                    tolerance = tol > 1.0m ? tol / 100.0m : tol;
                }

                var productId = products[sku];
                var uomId = uoms[uomCode];

                var existingItem = order.Items.FirstOrDefault(i => i.ItemId == productId && i.UomId == uomId);
                if (existingItem != null)
                {
                    if (existingItem.ReceivedQty > 0)
                    {
                        throw new InvalidOperationException("IMPORT_TARGET_STATE_INVALID");
                    }

                    existingItem.ExpectedQty = expectedQty;
                    existingItem.Tolerance = tolerance;
                }
                else
                {
                    var newItem = new InboundOrderItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        InboundOrderId = orderId,
                        ItemId = productId,
                        UomId = uomId,
                        ExpectedQty = expectedQty,
                        ReceivedQty = 0,
                        Tolerance = tolerance
                    };
                    _inboundDb.InboundOrderItems.Add(newItem);
                }
            }

            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = username;

            await _inboundDb.SaveChangesAsync(cancellationToken);

            await _batchCoordinator.MarkCommittedAsync(batchId, username, cancellationToken);
            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return new ImportResultDto(true, batchId, "INBOUND_LINES", "COMMITTED", claimResult.Batch!.TotalRows, claimResult.Batch.SuccessRows, 0, new List<ImportRowErrorDto>(), null, orderId);
        }
        catch (Exception ex)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            _inboundDb.ChangeTracker.Clear();
            _masterDb.ChangeTracker.Clear();
            await _batchCoordinator.MarkFailedAsync(batchId, ex.Message, cancellationToken);
            var error = ex is InvalidOperationException { Message: "IMPORT_TARGET_STATE_INVALID" }
                ? "IMPORT_TARGET_STATE_INVALID"
                : $"Commit thất bại: {ex.Message}";
            return new ImportResultDto(false, batchId, "INBOUND_LINES", "FAILED", claimResult.Batch!.TotalRows, 0, claimResult.Batch.ErrorRows, new List<ImportRowErrorDto>(), error, orderId);
        }
    }

    public async Task<string?> ExportErrorCsvAsync(Guid orderId, Guid batchId, string username, CancellationToken cancellationToken)
    {
        var batch = await _masterDb.ImportBatches.FirstOrDefaultAsync(b =>
            b.Id == batchId && b.ImportType == "INBOUND_LINES" && b.TargetId == orderId && b.CreatedBy == username,
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
