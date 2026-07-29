using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Entities;

namespace Nexustock.Modules.MasterData.Services;

public interface IImportService
{
    Task<ImportResultDto> PreviewImportAsync(string importType, string csvContent, string username, CancellationToken cancellationToken);
    Task<ImportResultDto> PreviewImportAsync(string importType, IReadOnlyList<string[]> rows, string username, CancellationToken cancellationToken);
    Task<ImportResultDto> CommitImportAsync(Guid batchId, string username, CancellationToken cancellationToken);
    Task<string?> ExportErrorCsvAsync(Guid batchId, string username, CancellationToken cancellationToken);
    string GetTemplateCsv(string importType);
}

public class ImportService : IImportService
{
    private readonly MasterDataDbContext _db;
    private readonly IImportBatchCoordinator _batchCoordinator;

    public ImportService(MasterDataDbContext db, IImportBatchCoordinator batchCoordinator)
    {
        _db = db;
        _batchCoordinator = batchCoordinator;
    }

    public string GetTemplateCsv(string importType)
    {
        var type = importType.Trim().ToUpperInvariant();
        return type switch
        {
            "ITEMS" => "code,name,baseUomCode,trackingPolicy,shelfLifeDays,minStock,errorMessage\n" +
                       "SP001,Sản phẩm mẫu 1,PCS,NONE,0,10,\n" +
                       "SP002,Sản phẩm mẫu 2,KG,BATCH,30,5,",
            "LOCATIONS" => "warehouseCode,zoneCode,code,xCoord,yCoord,zCoord,maxCapacity,errorMessage\n" +
                           "K01,Z01,LOC-01,1,1,1,1000,\n" +
                           "K01,Z01,LOC-02,1,1,2,1000,",
            "PARTNERS" => "code,name,partnerType,address,taxCode,errorMessage\n" +
                          "NCC01,Nhà cung cấp mẫu,VENDOR,123 Đường A,0102030405,\n" +
                          "KH01,Khách hàng mẫu,CUSTOMER,456 Đường B,0908070605,",
            "UOMS" => "code,name,isActive,errorMessage\n" +
                      "PCS,Cái,TRUE,\n" +
                      "BOX,Hộp,TRUE,",
            "WAREHOUSES" => "code,name,description,isActive,errorMessage\n" +
                            "K01,Kho chính,Kho trung tâm của hệ thống,TRUE,\n" +
                            "K02,Kho phụ,Kho lưu trữ hàng dự phòng,TRUE,",
            "ZONES" => "warehouseCode,code,name,zoneType,errorMessage\n" +
                       "K01,Z01,Vùng lưu trữ thường,STORAGE,\n" +
                       "K01,Z02,Vùng nhập hàng,RECEIVING,",
            "REASONS" => "code,reasonType,description,isActive,errorMessage\n" +
                         "ADJ-COUNT,ADJUSTMENT,Điều chỉnh sau kiểm kê,TRUE,\n" +
                         "RMA-DEFECT,RMA,Hàng trả lại bị lỗi,TRUE,",
            "PACKAGES" => "productCode,packageName,barcode,uomCode,conversionFactor,isActive,errorMessage\n" +
                          "SP001,Hộp 10 cái,BAR-BOX-01,BOX,10,TRUE,\n" +
                          "SP001,Thùng 100 cái,BAR-CTN-01,CTN,100,TRUE,",
            _ => throw new ArgumentException("Loại import không hợp lệ.")
        };
    }

    public async Task<ImportResultDto> PreviewImportAsync(string importType, string csvContent, string username, CancellationToken cancellationToken)
    {
        var rawRows = CsvParser.Parse(csvContent);
        return await PreviewImportAsync(importType, rawRows, username, cancellationToken);
    }

    public async Task<ImportResultDto> PreviewImportAsync(string importType, IReadOnlyList<string[]> rows, string username, CancellationToken cancellationToken)
    {
        var type = importType.Trim().ToUpperInvariant();
        if (rows.Count <= 1)
        {
            return new ImportResultDto(false, Guid.Empty, type, "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "File trống hoặc chỉ có header.");
        }

        var dataRowCount = rows.Count - 1;
        if (dataRowCount > SpreadsheetReader.MaxDataRows)
        {
            return new ImportResultDto(false, Guid.Empty, type, "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "IMPORT_TOO_LARGE");
        }

        var rawRows = rows.ToList();
        var header = rawRows[0].Select(h => h.Trim()).ToArray();
        var dataRows = rawRows.Skip(1).ToList();
        var batchId = Guid.NewGuid();
        var tenantId = _db.CurrentTenantId;

        var batch = new ImportBatch
        {
            Id = batchId,
            TenantId = tenantId,
            ImportType = type,
            Status = type == "PACKAGES" ? "PREVIEWED" : "VALIDATED",
            TotalRows = dataRows.Count,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = username,
            ExpiresAt = type == "PACKAGES" ? DateTimeOffset.UtcNow.AddHours(24) : null
        };

        var errors = new List<ImportRowErrorDto>();
        var batchRows = new List<ImportBatchRow>();

        // Cache existing data for faster validation
        var existingUomCodes = await _db.Uoms.Select(x => x.Code).ToListAsync(cancellationToken);
        var existingWarehouseCodes = await _db.Warehouses.Select(x => x.Code).ToListAsync(cancellationToken);
        var existingZoneCodes = await _db.StorageZones.Select(x => new { x.Code, WhCode = x.Warehouse!.Code }).ToListAsync(cancellationToken);
        var existingProductCodes = await _db.Products.Select(x => x.Code).ToListAsync(cancellationToken);
        var existingLocationCodes = await _db.StorageLocations.Select(x => x.Code).ToListAsync(cancellationToken);
        var existingPartnerCodes = await _db.Partners.Select(x => x.Code).ToListAsync(cancellationToken);
        var existingReasonCodes = await _db.ReasonCodes.Select(x => x.Code).ToListAsync(cancellationToken);
        var existingProductsMap = await _db.Products.ToDictionaryAsync(x => x.Code.ToUpperInvariant(), x => x.IsActive, cancellationToken);
        var existingUomsMap = await _db.Uoms.ToDictionaryAsync(x => x.Code.ToUpperInvariant(), x => x.IsActive, cancellationToken);
        var existingPackageBarcodes = (await _db.Packages.Where(x => !string.IsNullOrEmpty(x.Barcode)).Select(x => x.Barcode!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingProductBarcodes = (await _db.Products.Where(x => !string.IsNullOrEmpty(x.Barcode)).Select(x => x.Barcode!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Keep track of codes within the file to check for duplicates
        var seenCodes = new HashSet<string>();
        var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            // Perform type specific validation
            if (type == "ITEMS")
            {
                var code = GetVal(row, header, "code")?.ToUpperInvariant();
                var name = GetVal(row, header, "name");
                var baseUomCode = GetVal(row, header, "baseUomCode")?.ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(code) || code.Length < 2 || code.Length > 50 || code.Contains(" "))
                {
                    isValid = false;
                    errorMsg.Append("Mã vật tư phải từ 2-50 ký tự, không dấu, không cách. ");
                }
                else if (existingProductCodes.Contains(code) || seenCodes.Contains(code))
                {
                    isValid = false;
                    errorMsg.Append($"Mã vật tư '{code}' đã tồn tại hoặc bị trùng trong file. ");
                }
                else
                {
                    seenCodes.Add(code);
                }

                if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
                {
                    isValid = false;
                    errorMsg.Append("Tên vật tư không được để trống và tối đa 255 ký tự. ");
                }

                if (string.IsNullOrWhiteSpace(baseUomCode) || !existingUomCodes.Contains(baseUomCode))
                {
                    isValid = false;
                    errorMsg.Append($"Đơn vị tính cơ sở '{baseUomCode}' không tồn tại. ");
                }

                var shelfLifeDaysStr = GetVal(row, header, "shelfLifeDays");
                if (!string.IsNullOrEmpty(shelfLifeDaysStr) && (!int.TryParse(shelfLifeDaysStr, out var days) || days < 0))
                {
                    isValid = false;
                    errorMsg.Append("Số ngày hạn dùng phải là số nguyên >= 0. ");
                }

                var minStockStr = GetVal(row, header, "minStock");
                if (!string.IsNullOrEmpty(minStockStr) && (!decimal.TryParse(minStockStr, out var minStock) || minStock < 0))
                {
                    isValid = false;
                    errorMsg.Append("Tồn kho tối thiểu phải >= 0. ");
                }
            }
            else if (type == "LOCATIONS")
            {
                var whCode = GetVal(row, header, "warehouseCode")?.ToUpperInvariant();
                var zoneCode = GetVal(row, header, "zoneCode")?.ToUpperInvariant();
                var code = GetVal(row, header, "code")?.ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(whCode) || !existingWarehouseCodes.Contains(whCode))
                {
                    isValid = false;
                    errorMsg.Append($"Mã kho '{whCode}' không tồn tại. ");
                }

                if (string.IsNullOrWhiteSpace(zoneCode) || !existingZoneCodes.Any(z => z.Code == zoneCode && z.WhCode == whCode))
                {
                    isValid = false;
                    errorMsg.Append($"Mã vùng kho '{zoneCode}' không tồn tại thuộc kho '{whCode}'. ");
                }

                if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                {
                    isValid = false;
                    errorMsg.Append("Mã vị trí không được để trống và tối đa 50 ký tự. ");
                }
                else if (existingLocationCodes.Contains(code) || seenCodes.Contains(code))
                {
                    isValid = false;
                    errorMsg.Append($"Mã vị trí '{code}' đã tồn tại hoặc bị trùng trong file. ");
                }
                else
                {
                    seenCodes.Add(code);
                }

                var xStr = GetVal(row, header, "xCoord");
                var yStr = GetVal(row, header, "yCoord");
                var zStr = GetVal(row, header, "zCoord");
                if (!string.IsNullOrEmpty(xStr) && (!int.TryParse(xStr, out var x) || x < 0)) { isValid = false; errorMsg.Append("Tọa độ X phải >= 0. "); }
                if (!string.IsNullOrEmpty(yStr) && (!int.TryParse(yStr, out var y) || y < 0)) { isValid = false; errorMsg.Append("Tọa độ Y phải >= 0. "); }
                if (!string.IsNullOrEmpty(zStr) && (!int.TryParse(zStr, out var z) || z < 0)) { isValid = false; errorMsg.Append("Tọa độ Z phải >= 0. "); }

                var maxCapStr = GetVal(row, header, "maxCapacity");
                if (!string.IsNullOrEmpty(maxCapStr) && (!decimal.TryParse(maxCapStr, out var cap) || cap < 0))
                {
                    isValid = false;
                    errorMsg.Append("Sức chứa tối đa phải >= 0. ");
                }
            }
            else if (type == "PARTNERS")
            {
                var code = GetVal(row, header, "code")?.ToUpperInvariant();
                var name = GetVal(row, header, "name");
                var partnerType = GetVal(row, header, "partnerType")?.ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                {
                    isValid = false;
                    errorMsg.Append("Mã đối tác không được để trống và tối đa 50 ký tự. ");
                }
                else if (existingPartnerCodes.Contains(code) || seenCodes.Contains(code))
                {
                    isValid = false;
                    errorMsg.Append($"Mã đối tác '{code}' đã tồn tại hoặc bị trùng trong file. ");
                }
                else
                {
                    seenCodes.Add(code);
                }

                if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
                {
                    isValid = false;
                    errorMsg.Append("Tên đối tác không được để trống và tối đa 255 ký tự. ");
                }

                if (string.IsNullOrWhiteSpace(partnerType) || (partnerType != "VENDOR" && partnerType != "CUSTOMER" && partnerType != "CARRIER"))
                {
                    isValid = false;
                    errorMsg.Append("Loại đối tác phải là VENDOR, CUSTOMER hoặc CARRIER. ");
                }
            }
            else if (type == "UOMS")
            {
                var code = GetVal(row, header, "code")?.ToUpperInvariant();
                var name = GetVal(row, header, "name");

                if (string.IsNullOrWhiteSpace(code) || code.Length > 20)
                {
                    isValid = false;
                    errorMsg.Append("Mã đơn vị tính không được để trống và tối đa 20 ký tự. ");
                }
                else if (existingUomCodes.Contains(code) || seenCodes.Contains(code))
                {
                    isValid = false;
                    errorMsg.Append($"Mã đơn vị tính '{code}' đã tồn tại hoặc bị trùng trong file. ");
                }
                else
                {
                    seenCodes.Add(code);
                }

                if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
                {
                    isValid = false;
                    errorMsg.Append("Tên đơn vị tính không được để trống và tối đa 100 ký tự. ");
                }
            }
            else if (type == "WAREHOUSES")
            {
                var code = GetVal(row, header, "code")?.ToUpperInvariant();
                var name = GetVal(row, header, "name");

                if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                {
                    isValid = false;
                    errorMsg.Append("Mã kho không được để trống và tối đa 50 ký tự. ");
                }
                else if (existingWarehouseCodes.Contains(code) || seenCodes.Contains(code))
                {
                    isValid = false;
                    errorMsg.Append($"Mã kho '{code}' đã tồn tại hoặc bị trùng trong file. ");
                }
                else
                {
                    seenCodes.Add(code);
                }

                if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
                {
                    isValid = false;
                    errorMsg.Append("Tên kho không được để trống và tối đa 150 ký tự. ");
                }
            }
            else if (type == "ZONES")
            {
                var whCode = GetVal(row, header, "warehouseCode")?.ToUpperInvariant();
                var code = GetVal(row, header, "code")?.ToUpperInvariant();
                var name = GetVal(row, header, "name");
                var zoneType = GetVal(row, header, "zoneType")?.ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(whCode) || !existingWarehouseCodes.Contains(whCode))
                {
                    isValid = false;
                    errorMsg.Append($"Mã kho '{whCode}' không tồn tại. ");
                }

                if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                {
                    isValid = false;
                    errorMsg.Append("Mã vùng không được để trống và tối đa 50 ký tự. ");
                }
                else
                {
                    var zoneKey = $"{whCode}_{code}";
                    if (existingZoneCodes.Any(z => z.Code == code && z.WhCode == whCode) || seenCodes.Contains(zoneKey))
                    {
                        isValid = false;
                        errorMsg.Append($"Vùng '{code}' đã tồn tại trong kho '{whCode}' hoặc bị trùng trong file. ");
                    }
                    else
                    {
                        seenCodes.Add(zoneKey);
                    }
                }

                if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
                {
                    isValid = false;
                    errorMsg.Append("Tên vùng không được để trống và tối đa 150 ký tự. ");
                }

                if (string.IsNullOrWhiteSpace(zoneType) || (zoneType != "STORAGE" && zoneType != "RECEIVING" && zoneType != "SHIPPING" && zoneType != "QC" && zoneType != "STAGE"))
                {
                    isValid = false;
                    errorMsg.Append("Loại vùng phải là STORAGE, RECEIVING, SHIPPING, QC hoặc STAGE. ");
                }
            }
            else if (type == "REASONS")
            {
                var code = GetVal(row, header, "code")?.ToUpperInvariant();
                var reasonType = GetVal(row, header, "reasonType")?.ToUpperInvariant();
                var desc = GetVal(row, header, "description");

                if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                {
                    isValid = false;
                    errorMsg.Append("Mã lý do không được để trống và tối đa 50 ký tự. ");
                }
                else if (existingReasonCodes.Contains(code) || seenCodes.Contains(code))
                {
                    isValid = false;
                    errorMsg.Append($"Mã lý do '{code}' đã tồn tại hoặc bị trùng trong file. ");
                }
                else
                {
                    seenCodes.Add(code);
                }

                if (string.IsNullOrWhiteSpace(reasonType) || (reasonType != "ADJUSTMENT" && reasonType != "RMA" && reasonType != "QC" && reasonType != "RETURN" && reasonType != "SCRAP"))
                {
                    isValid = false;
                    errorMsg.Append("Loại lý do phải là ADJUSTMENT, RMA, QC, RETURN hoặc SCRAP. ");
                }

                if (string.IsNullOrWhiteSpace(desc) || desc.Length > 255)
                {
                    isValid = false;
                    errorMsg.Append("Mô tả lý do không được để trống và tối đa 255 ký tự. ");
                }
            }
            else if (type == "PACKAGES")
            {
                var productCode = GetVal(row, header, "productCode")?.ToUpperInvariant();
                var packageName = GetVal(row, header, "packageName");
                var barcode = GetVal(row, header, "barcode");
                var uomCode = GetVal(row, header, "uomCode")?.ToUpperInvariant();
                var factorStr = GetVal(row, header, "conversionFactor");

                if (string.IsNullOrWhiteSpace(productCode) || !existingProductsMap.TryGetValue(productCode, out var isProdActive))
                {
                    isValid = false;
                    errorMsg.Append($"Mã sản phẩm '{productCode}' không tồn tại trong hệ thống. ");
                }
                else if (!isProdActive)
                {
                    isValid = false;
                    errorMsg.Append($"Sản phẩm '{productCode}' đang bị khóa (không hoạt động). ");
                }

                if (string.IsNullOrWhiteSpace(packageName) || packageName.Length > 100)
                {
                    isValid = false;
                    errorMsg.Append("Tên quy cách đóng gói không được để trống và tối đa 100 ký tự. ");
                }

                if (string.IsNullOrWhiteSpace(uomCode) || !existingUomsMap.TryGetValue(uomCode, out var isUomActive))
                {
                    isValid = false;
                    errorMsg.Append($"Đơn vị tính '{uomCode}' không tồn tại trong hệ thống. ");
                }
                else if (!isUomActive)
                {
                    isValid = false;
                    errorMsg.Append($"Đơn vị tính '{uomCode}' đang bị khóa (không hoạt động). ");
                }

                if (!string.IsNullOrWhiteSpace(productCode) && !string.IsNullOrWhiteSpace(uomCode))
                {
                    var comboKey = $"{productCode}_{uomCode}";
                    if (seenCodes.Contains(comboKey))
                    {
                        isValid = false;
                        errorMsg.Append($"Quy cách đóng gói cho sản phẩm '{productCode}' và ĐVT '{uomCode}' bị trùng lặp trong file. ");
                    }
                    else
                    {
                        seenCodes.Add(comboKey);
                    }
                }

                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    if (barcode.Length > 100)
                    {
                        isValid = false;
                        errorMsg.Append("Mã vạch tối đa 100 ký tự. ");
                    }
                    else if (existingPackageBarcodes.Contains(barcode) || existingProductBarcodes.Contains(barcode) || seenBarcodes.Contains(barcode))
                    {
                        isValid = false;
                        errorMsg.Append($"Mã vạch '{barcode}' đã tồn tại trong hệ thống hoặc trùng trong file. ");
                    }
                    else
                    {
                        seenBarcodes.Add(barcode);
                    }
                }

                if (string.IsNullOrWhiteSpace(factorStr) || !decimal.TryParse(factorStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var factor) || factor <= 0)
                {
                    isValid = false;
                    errorMsg.Append("Hệ số quy đổi phải là số lớn hơn 0. ");
                }
            }
            else
            {
                isValid = false;
                errorMsg.Append("Loại import không hợp lệ.");
            }

            var finalError = errorMsg.ToString().Trim();
            if (!isValid)
            {
                errors.Add(new ImportRowErrorDto(rowIndex, rawMap, finalError));
            }

            batchRows.Add(new ImportBatchRow
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                RowIndex = rowIndex,
                RawData = JsonSerializer.Serialize(rawMap),
                IsValid = isValid,
                ErrorMessage = isValid ? null : finalError
            });
        }

        batch.SuccessRows = batchRows.Count(x => x.IsValid);
        batch.ErrorRows = batchRows.Count(x => !x.IsValid);

        await _db.ImportBatches.AddAsync(batch, cancellationToken);
        await _db.ImportBatchRows.AddRangeAsync(batchRows, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new ImportResultDto(
            errors.Count == 0,
            batchId,
            type,
            batch.Status,
            batch.TotalRows,
            batch.SuccessRows,
            batch.ErrorRows,
            errors,
            null
        );
    }

    public async Task<ImportResultDto> CommitImportAsync(Guid batchId, string username, CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .IgnoreQueryFilters() // Cần tìm batch bất kể filter tenant khi xử lý hệ thống
            .FirstOrDefaultAsync(x => x.Id == batchId && x.TenantId == _db.CurrentTenantId, cancellationToken);

        if (batch is null)
        {
            return new ImportResultDto(false, batchId, "UNKNOWN", "FAILED", 0, 0, 0, new List<ImportRowErrorDto>(), "IMPORT_BATCH_NOT_FOUND");
        }

        if (batch.ImportType == "PACKAGES")
        {
            var claim = await _batchCoordinator.ClaimBatchForCommitAsync(
                batchId, _db.CurrentTenantId, "PACKAGES", null, username, cancellationToken);
            if (claim.Status != BatchClaimStatus.Success)
            {
                return new ImportResultDto(false, batchId, batch.ImportType, claim.Batch?.Status ?? "FAILED",
                    claim.Batch?.TotalRows ?? 0, claim.Batch?.SuccessRows ?? 0, claim.Batch?.ErrorRows ?? 0,
                    new List<ImportRowErrorDto>(), claim.ErrorMessage);
            }
            batch = claim.Batch!;
        }
        else
        {
            if (batch.Status != "VALIDATED")
            {
                return new ImportResultDto(false, batchId, batch.ImportType, batch.Status, batch.TotalRows, batch.SuccessRows, batch.ErrorRows, new List<ImportRowErrorDto>(), $"Phiên nhập dữ liệu ở trạng thái '{batch.Status}' không thể duyệt.");
            }

            if (batch.ErrorRows > 0)
            {
                return new ImportResultDto(false, batchId, batch.ImportType, batch.Status, batch.TotalRows, batch.SuccessRows, batch.ErrorRows, new List<ImportRowErrorDto>(), "Không thể duyệt phiên nhập dữ liệu có lỗi.");
            }

            batch.Status = "PROCESSING";
            await _db.SaveChangesAsync(cancellationToken);
        }

        var rows = await _db.ImportBatchRows
            .Where(x => x.BatchId == batchId && x.IsValid)
            .OrderBy(x => x.RowIndex)
            .ToListAsync(cancellationToken);

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (batch.ImportType == "ITEMS")
            {
                var uoms = await _db.Uoms.ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
                foreach (var row in rows)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData!)!;
                    var code = map["code"].Trim().ToUpperInvariant();
                    var name = map["name"].Trim();
                    var baseUomCode = map["baseUomCode"].Trim().ToUpperInvariant();
                    var trackingPolicy = map.TryGetValue("trackingPolicy", out var tp) && !string.IsNullOrEmpty(tp) ? tp.Trim().ToUpperInvariant() : "NONE";
                    var shelfLifeDays = map.TryGetValue("shelfLifeDays", out var sld) && int.TryParse(sld, out var d) ? d : 0;
                    var minStock = map.TryGetValue("minStock", out var ms) && decimal.TryParse(ms, out var m) ? m : 0.0000m;

                    var productId = Guid.NewGuid();
                    var product = new Product
                    {
                        Id = productId,
                        TenantId = batch.TenantId,
                        Code = code,
                        Name = name,
                        BaseUomId = uoms[baseUomCode],
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    var config = new ProductConfig
                    {
                        ProductId = productId,
                        TenantId = batch.TenantId,
                        IqcCheckType = "FULL",
                        MinStock = minStock,
                        WeightClass = "MEDIUM",
                        RotationSpeed = "SLOW"
                    };

                    await _db.Products.AddAsync(product, cancellationToken);
                    await _db.ProductConfigs.AddAsync(config, cancellationToken);
                }
            }
            else if (batch.ImportType == "LOCATIONS")
            {
                var zones = await _db.StorageZones.Include(x => x.Warehouse).ToDictionaryAsync(x => x.Warehouse!.Code + "_" + x.Code, x => x.Id, cancellationToken);
                foreach (var row in rows)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData!)!;
                    var whCode = map["warehouseCode"].Trim().ToUpperInvariant();
                    var zoneCode = map["zoneCode"].Trim().ToUpperInvariant();
                    var code = map["code"].Trim().ToUpperInvariant();
                    var x = map.TryGetValue("xCoord", out var xs) && int.TryParse(xs, out var xv) ? xv : 0;
                    var y = map.TryGetValue("yCoord", out var ys) && int.TryParse(ys, out var yv) ? yv : 0;
                    var z = map.TryGetValue("zCoord", out var zs) && int.TryParse(zs, out var zv) ? zv : 0;
                    var cap = map.TryGetValue("maxCapacity", out var cs) && decimal.TryParse(cs, out var cv) ? cv : 999999.0000m;

                    var location = new StorageLocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = batch.TenantId,
                        ZoneId = zones[whCode + "_" + zoneCode],
                        Code = code,
                        XCoord = x,
                        YCoord = y,
                        ZCoord = z,
                        MaxCapacity = cap,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    await _db.StorageLocations.AddAsync(location, cancellationToken);
                }
            }
            else if (batch.ImportType == "PARTNERS")
            {
                foreach (var row in rows)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData!)!;
                    var code = map["code"].Trim().ToUpperInvariant();
                    var name = map["name"].Trim();
                    var partnerType = map["partnerType"].Trim().ToUpperInvariant();
                    var address = map.TryGetValue("address", out var addr) ? addr.Trim() : null;
                    var taxCode = map.TryGetValue("taxCode", out var tc) ? tc.Trim() : null;

                    var partner = new Partner
                    {
                        Id = Guid.NewGuid(),
                        TenantId = batch.TenantId,
                        Code = code,
                        Name = name,
                        PartnerType = partnerType,
                        Address = string.IsNullOrEmpty(address) ? null : address,
                        TaxCode = string.IsNullOrEmpty(taxCode) ? null : taxCode,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    await _db.Partners.AddAsync(partner, cancellationToken);
                }
            }
            else if (batch.ImportType == "UOMS")
            {
                foreach (var row in rows)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData!)!;
                    var code = map["code"].Trim().ToUpperInvariant();
                    var name = map["name"].Trim();
                    var isActiveVal = !map.TryGetValue("isActive", out var ia) || !bool.TryParse(ia, out var active) || active;

                    var uom = new Uom
                    {
                        Id = Guid.NewGuid(),
                        TenantId = batch.TenantId,
                        Code = code,
                        Name = name,
                        IsActive = isActiveVal,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    await _db.Uoms.AddAsync(uom, cancellationToken);
                }
            }
            else if (batch.ImportType == "WAREHOUSES")
            {
                foreach (var row in rows)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData!)!;
                    var code = map["code"].Trim().ToUpperInvariant();
                    var name = map["name"].Trim();
                    var desc = map.TryGetValue("description", out var d) ? d.Trim() : null;
                    var isActiveVal = !map.TryGetValue("isActive", out var ia) || !bool.TryParse(ia, out var active) || active;

                    var warehouse = new Warehouse
                    {
                        Id = Guid.NewGuid(),
                        TenantId = batch.TenantId,
                        Code = code,
                        Name = name,
                        Description = string.IsNullOrEmpty(desc) ? null : desc,
                        IsActive = isActiveVal,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    await _db.Warehouses.AddAsync(warehouse, cancellationToken);
                }
            }
            else if (batch.ImportType == "ZONES")
            {
                var warehouses = await _db.Warehouses.ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
                foreach (var row in rows)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData!)!;
                    var whCode = map["warehouseCode"].Trim().ToUpperInvariant();
                    var code = map["code"].Trim().ToUpperInvariant();
                    var name = map["name"].Trim();
                    var zoneType = map.TryGetValue("zoneType", out var zt) ? zt.Trim().ToUpperInvariant() : "STORAGE";

                    var zone = new StorageZone
                    {
                        Id = Guid.NewGuid(),
                        TenantId = batch.TenantId,
                        WarehouseId = warehouses[whCode],
                        Code = code,
                        Name = name,
                        ZoneType = zoneType,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    await _db.StorageZones.AddAsync(zone, cancellationToken);
                }
            }
            else if (batch.ImportType == "REASONS")
            {
                foreach (var row in rows)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData!)!;
                    var code = map["code"].Trim().ToUpperInvariant();
                    var reasonType = map["reasonType"].Trim().ToUpperInvariant();
                    var desc = map["description"].Trim();
                    var isActiveVal = !map.TryGetValue("isActive", out var ia) || !bool.TryParse(ia, out var active) || active;

                    var reason = new ReasonCode
                    {
                        Id = Guid.NewGuid(),
                        TenantId = batch.TenantId,
                        Code = code,
                        ReasonType = reasonType,
                        Description = desc,
                        IsActive = isActiveVal,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    await _db.ReasonCodes.AddAsync(reason, cancellationToken);
                }
            }
            else if (batch.ImportType == "PACKAGES")
            {
                var products = await _db.Products.ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
                var uoms = await _db.Uoms.ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
                var existingPkgs = await _db.Packages.ToListAsync(cancellationToken);
                var pkgMap = existingPkgs.ToDictionary(x => $"{x.ProductId}_{x.UomId}", x => x);

                foreach (var row in rows)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData!)!;
                    var productCode = map["productCode"].Trim().ToUpperInvariant();
                    var packageName = map["packageName"].Trim();
                    var barcode = map.TryGetValue("barcode", out var bc) && !string.IsNullOrWhiteSpace(bc) ? bc.Trim() : null;
                    var uomCode = map["uomCode"].Trim().ToUpperInvariant();
                    var factor = map.TryGetValue("conversionFactor", out var fs) && decimal.TryParse(fs, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 1.0000m;
                    var isActiveVal = !map.TryGetValue("isActive", out var ia) || !bool.TryParse(ia, out var active) || active;

                    var productId = products[productCode];
                    var uomId = uoms[uomCode];
                    var comboKey = $"{productId}_{uomId}";

                    if (pkgMap.TryGetValue(comboKey, out var existingPkg))
                    {
                        existingPkg.PackageName = packageName;
                        existingPkg.Barcode = barcode;
                        existingPkg.ConversionFactor = factor;
                        existingPkg.IsActive = isActiveVal;
                        existingPkg.UpdatedAt = DateTimeOffset.UtcNow;
                        existingPkg.UpdatedBy = batch.CreatedBy ?? "SYSTEM";
                        existingPkg.RowVersion++;
                    }
                    else
                    {
                        var newPkg = new Package
                        {
                            Id = Guid.NewGuid(),
                            TenantId = batch.TenantId,
                            ProductId = productId,
                            PackageName = packageName,
                            Barcode = barcode,
                            UomId = uomId,
                            ConversionFactor = factor,
                            IsActive = isActiveVal,
                            CreatedAt = DateTimeOffset.UtcNow,
                            CreatedBy = batch.CreatedBy ?? "SYSTEM",
                            RowVersion = 1
                        };
                        await _db.Packages.AddAsync(newPkg, cancellationToken);
                        pkgMap[comboKey] = newPkg;
                    }
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            batch.Status = "COMMITTED";
            batch.CommittedAt = DateTimeOffset.UtcNow;
            batch.CommittedBy = username;
            batch.RowVersion++;
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new ImportResultDto(true, batchId, batch.ImportType, batch.Status, batch.TotalRows, batch.SuccessRows, batch.ErrorRows, new List<ImportRowErrorDto>(), null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);

            batch.Status = "FAILED";
            await _db.SaveChangesAsync(cancellationToken);

            return new ImportResultDto(false, batchId, batch.ImportType, batch.Status, batch.TotalRows, batch.SuccessRows, batch.ErrorRows, new List<ImportRowErrorDto>(), $"Commit thất bại: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<string?> ExportErrorCsvAsync(Guid batchId, string username, CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(x =>
            x.Id == batchId && x.TenantId == _db.CurrentTenantId && x.CreatedBy == username,
            cancellationToken);
        if (batch is null) return null;

        var rows = await _db.ImportBatchRows
            .Where(x => x.BatchId == batchId && !x.IsValid)
            .OrderBy(x => x.RowIndex)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return null;

        var firstRow = JsonSerializer.Deserialize<Dictionary<string, string>>(rows[0].RawData!)!;
        var header = firstRow.Keys.ToList();

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine(string.Join(",", header.Select(EscapeCsvField)) + ",errorMessage");

        foreach (var r in rows)
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(r.RawData!)!;
            var rowFields = new List<string>();
            foreach (var h in header)
            {
                rowFields.Add(map.TryGetValue(h, out var v) ? v : string.Empty);
            }
            rowFields.Add(r.ErrorMessage ?? string.Empty);
            csvBuilder.AppendLine(string.Join(",", rowFields.Select(EscapeCsvField)));
        }

        return csvBuilder.ToString();
    }

    private static string? GetVal(string[] row, string[] header, string key)
    {
        var idx = Array.FindIndex(header, h => h.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && idx < row.Length) return row[idx].Trim();
        return null;
    }

    private static string EscapeCsvField(string field)
    {
        var sanitized = SpreadsheetReader.SanitizeFormula(field);
        if (sanitized.Contains(",") || sanitized.Contains("\"") || sanitized.Contains("\n") || sanitized.Contains("\r"))
        {
            return "\"" + sanitized.Replace("\"", "\"\"") + "\"";
        }
        return sanitized;
    }
}

public static class CsvParser
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

