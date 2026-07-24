# PHASE 46D: Package IE + Idempotent Operational Line Imports

## Execution Spec

| Mục | Giá trị |
|---|---|
| Trạng thái | ⬜ Spec Ready |
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
POST /api/inbound/{orderId}/lines/import/preview
POST /api/inbound/{orderId}/lines/import/commit
```

Template v1:

```text
lineNo,sku,qty,lotNo,uomCode,errorMessage
```

Validation:

- Inbound order tồn tại trong tenant và trạng thái cho phép sửa.
- SKU/Product/UOM tồn tại, active.
- Qty > 0; precision đúng UOM.
- Lot required/format theo product policy.
- Duplicate line trong file được phát hiện.
- Không ghi đè line đã received.
- Commit all-or-nothing; audit batch ID trên kết quả nếu schema cho phép.

## 5. Stocktake Count Line Import

```http
POST /api/stocktakes/{stocktakeId}/lines/import/preview
POST /api/stocktakes/{stocktakeId}/lines/import/commit
```

Template v1:

```text
lineNo,locationCode,sku,lotNo,countQty,uomCode,errorMessage
```

Validation:

- Stocktake tồn tại trong tenant và open/counting.
- Location/Product/UOM/Lot tồn tại.
- countQty >= 0 và precision hợp lệ.
- Duplicate natural key trong file bị chặn.
- Chặn commit sau close/cancel/freeze transition.
- Không tự post adjustment ngoài workflow stocktake hiện có.

## 6. Permissions

EP0 phải resolve tên permission thật trước code. Contract mục tiêu:

| Hành động | Permission |
|---|---|
| Package preview/commit | `master_data.import` |
| Package export | `master_data.export` |
| Inbound line preview/commit | `inbound.lines.import` hoặc permission inbound import hiện hữu |
| Stocktake count preview/commit | permission stocktake count/import hiện hữu |

Không seed permission mới nếu permission tương đương đã tồn tại. Backend enforce; UI chỉ phản ánh.

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

- [ ] Package IE CSV/XLSX roundtrip pass.
- [ ] Inbound line import preview/commit pass.
- [ ] Stocktake count import preview/commit pass.
- [ ] Batch TTL/ownership/concurrency/idempotency pass.
- [ ] Permission/tenant/state gates pass.
- [ ] Error CSV và UI wizard pass.
- [ ] `tests/verify_package_line_imports_p46d.ps1` pass.

## 11. Rollback

Tắt route/UI import mới. Batch preview/audit giữ lại. Không tự xóa domain rows đã commit; rollback nghiệp vụ dùng transaction khi commit fail hoặc compensating workflow đã có.
