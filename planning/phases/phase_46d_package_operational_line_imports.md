# PHASE 46D: Package IE + Idempotent Operational Line Imports

## Execution Spec

| Mục | Giá trị |
|---|---|
| Trạng thái | ✅ Done — tái nghiệm thu `rp4`/`rp5` ngày 2026-07-29 |
| Bằng chứng | Strict verifier PASS · 84/84 integration tests · TypeScript/ESLint PASS |
| Ước lượng | 3–4 dev-days |
| Upstream | P43 spreadsheet foundation |
| Downstream | P46E |
| Scope nguồn | P45 Package IE + ASN/Pick line import requirement |

## 1. Mục tiêu

Hoàn tất Package CSV/XLSX và hai operational line imports. Chọn `Inbound ASN` là P0 và `Stocktake count` là P1 hợp lệ theo P45 (`Pick hoặc Stocktake`).

## 2. Shared Batch Contract

Batch fields tối thiểu:

```text
id,tenantId,type,status,fileName,fileHash,totalRows,validRows,errorRows,
createdBy,createdAt,expiresAt,committedBy,committedAt
```

Statuses:

```text
PREVIEWED → COMMITTING → COMMITTED
          ↘ FAILED
          ↘ EXPIRED
```

Rules:

- Preview không ghi domain data.
- Batch tenant/user ownership được kiểm tra.
- TTL 24 giờ.
- File hash + type dùng phát hiện duplicate preview, không tự commit.
- Commit transaction.
- Concurrent commit dùng atomic status/row version; chỉ một request thắng.
- Recommit `COMMITTED` trả 409 `IMPORT_BATCH_ALREADY_COMMITTED`.
- Error CSV UTF-8 BOM.
- Cap 5.000 rows; template version `v1` bắt buộc.

## 3. Package Import/Export

Route reuse Master hub:

```http
POST /api/imports?type=PACKAGES
POST /api/imports/{batchId}/commit
GET  /api/exports?type=PACKAGES&format=csv|xlsx
```

Columns:

```text
productCode,packageName,barcode,uomCode,conversionFactor,errorMessage
```

Validation:

- Product tồn tại trong tenant.
- UOM tồn tại/active.
- Barcode hợp lệ và unique theo rule thật.
- Package name bắt buộc.
- Conversion factor > 0.
- Natural key/idempotent upsert khóa theo schema thật.

Roundtrip CSV/XLSX bắt buộc.

## 4. Inbound ASN Line Import

```http
POST /api/inbound/{id}/lines/import/preview
POST /api/inbound/{id}/lines/import/commit
GET  /api/inbound/{id}/lines/import/errors/{batchId}
```

Template v1:

```text
sku,uomCode,expectedQty,tolerance,errorMessage
```

Validation:

- Inbound order tồn tại trong tenant và trạng thái `Draft|Open`.
- SKU/Product/UOM tồn tại, active; UOM thuộc Base UOM hoặc Package UOM hợp lệ của Product.
- `expectedQty > 0`; `tolerance >= 0`.
- Duplicate `(sku,uomCode)` trong file bị chặn.
- Không sửa/xóa line đã có `receivedQty > 0`.
- Commit atomic qua transaction.

## 5. Stocktake Count Line Import

```http
POST /api/stocktakes/{id}/lines/import/preview
POST /api/stocktakes/{id}/lines/import/commit
GET  /api/stocktakes/{id}/lines/import/errors/{batchId}
```

Template v1:

```text
lineNo,locationCode,sku,lotNo,countQty,uomCode,errorMessage
```

Validation:

- Stocktake tồn tại trong tenant và trạng thái `Counting`.
- Location thuộc Zone của Stocktake (nếu zoneId có giá trị); Product/Location/Lot hợp lệ.
- `uomCode` bắt buộc khớp Base UOM của Product.
- `countQty >= 0`; zero count hợp lệ.
- Duplicate `(locationCode,sku,lotNo)` trong file bị chặn.
- Cập nhật/tạo `StocktakeItem` với `status = Counted`; không tạo `StockAdjustment`.

## 6. Permissions

Permissions thật trong hệ thống:

| Hành động | Permission |
|---|---|
| Package preview/commit | `master_data.import` |
| Package template/export | `master_data.export` |
| Inbound line preview/commit | `Inbound.Orders.Create` |
| Stocktake count preview/commit | `Inventory.CycleCount.Count` |

Backend enforce via UserPermissionService; UI kiểm tra theo quyền tương ứng.

## 7. Frontend

- Package tại Master import/export hub.
- Inbound detail và Stocktake detail có Import action.
- Wizard: chọn file → preview summary/table → tải lỗi → commit → result.
- Commit disabled khi errorRows > 0, expired, wrong status hoặc thiếu quyền.
- Retry preview/commit rõ; không double-submit.
- VI/EN parity, keyboard/focus, responsive.

## 8. Error Contract

| Code | HTTP |
|---|---:|
| `IMPORT_FILE_INVALID` | 400 |
| `IMPORT_TEMPLATE_VERSION_UNSUPPORTED` | 400 |
| `IMPORT_ROW_LIMIT_EXCEEDED` | 400 |
| `IMPORT_BATCH_NOT_FOUND` | 404 |
| `IMPORT_BATCH_EXPIRED` | 409 |
| `IMPORT_BATCH_HAS_ERRORS` | 409 |
| `IMPORT_BATCH_ALREADY_COMMITTED` | 409 |
| `IMPORT_TARGET_STATE_INVALID` | 409 |

Row errors có `rowNumber`, `column`, `code`, `message`.

## 9. Tests

- Package CSV/XLSX valid/invalid/roundtrip.
- Inbound valid/invalid/mixed/received-line/invalid-state.
- Stocktake zero count/negative/duplicate/invalid-state.
- Preview DB unchanged.
- Commit all-or-nothing.
- Concurrent commit và recommit.
- TTL/ownership/cross-tenant.
- 5.001 row rejection.
- Unicode/error CSV.
- FE double-submit prevention.

## 10. Definition of Done

- [x] Package IE CSV/XLSX roundtrip pass.
- [x] Inbound line import preview/commit pass.
- [x] Stocktake count import preview/commit pass.
- [x] Batch TTL/ownership/concurrency/idempotency pass.
- [x] Permission/tenant/state gates pass.
- [x] Error CSV và UI wizard pass.
- [x] `tests/verify_package_line_imports_p46d.ps1` pass.

## 11. Rollback

Tắt route/UI import mới. Batch preview/audit giữ lại. Không tự xóa domain rows đã commit; rollback nghiệp vụ dùng transaction khi commit fail hoặc compensating workflow đã có.
