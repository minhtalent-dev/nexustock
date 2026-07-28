# PHASE 46: Attachment Experience + Ops Spreadsheet Completion

## Execution Spec Maturity

| Mục | Giá trị |
|---|---|
| **Mức hiện tại** | **100% Scope Mapped · 95% Execution Ready** |
| **Option** | **B** — authenticated content API + shared preview + complete P43–P45 gaps |
| **Trạng thái** | ⏳ **Umbrella Spec** · thực thi qua P46A–P46E |
| **Dev-days** | **14–18** (1 Dev) |
| **Upstream** | P41 · P42 · P43 **ĐÓNG** |
| **Supersedes** | P44 · P45 cho mục đích triển khai/nghiệm thu; giữ tài liệu lịch sử |
| **Child phases** | [P46A](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46a_secure_attachment_content_preview.md) · [P46B](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46b_thumbnail_full_attachment_coverage.md) · [P46C](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46c_master_spreadsheet_full_ops_exports.md) · [P46D](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46d_package_operational_line_imports.md) · [P46E](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46e_rf_full_acceptance.md) |
| **Port FE** | `http://localhost:3003` |

### Changelog plan

| Ngày | Thay đổi |
|---|---|
| 2026-07-24 | Tạo Phase 46 hợp nhất toàn bộ phạm vi P43–P45 và sửa trải nghiệm attachment |
| 2026-07-24 | Xác minh root cause URL `/uploads` mở trên frontend origin gây 404 |
| 2026-07-24 | Tách execution thành P46A–P46E; bổ sung pending cleanup, QC compatibility, permission/column/batch contracts và zero-gap acceptance |
| 2026-07-25 | `rp4` + `rp5`: xác nhận P46A hoàn thành; static/parity/build Release/browser UAT pass; umbrella P46 tiếp tục mở cho P46B–P46E |
| 2026-07-27 | Tái xác nhận P46A: strict gate 21/21, Release build 0 lỗi/0 cảnh báo, UAT video được lưu; umbrella P46 vẫn mở đúng phạm vi |
| 2026-07-27 | Thu hồi trạng thái đóng P46B sau `rp4` + `rp5`: mở remediation cho WebP, log redaction, backfill race-safe và test/evidence còn thiếu; umbrella tiếp tục mở |
| 2026-07-27 | P46B remediation PASS: strict automated gates + browser evidence hoàn tất; P46B đóng lại Module DoD 100%, umbrella vẫn mở đến P46E |
| 2026-07-28 | `rp1`: Phase 46C đạt 100% Execution-Ready; khóa 12 Ops export contracts, phân quyền P0, EPs và DoD |
| 2026-07-28 | `rp2`: Reindex mã nguồn hàm/controller/UI, lập kế hoạch thực thi chi tiết chuẩn 100% qua `[17-auto-plan]` cho Phase 46C |
| 2026-07-28 | Phase 46C đóng 100%: strict verifier PASS, 76/76 integration tests, frontend typecheck/lint sạch; umbrella tiếp tục chờ P46D/P46E |

### Quyết định khóa

| Mục | Quyết định |
|---|---|
| Preview | Ảnh và PDF inline trong dialog; loại khác tải xuống |
| Content transport | API có JWT/RBAC/tenant, fetch blob, object URL được revoke |
| Storage | Đọc qua `IObjectStorageProvider.OpenReadAsync`; không phụ thuộc physical `/uploads` |
| Download | Content endpoint với `disposition=attachment` |
| Thumbnail | JPEG quality 82, max edge 256 px, key `{originalStorageKey}.thumb.jpg`; lỗi thumb không fail upload gốc |
| PDF thumbnail | Không bắt buộc; browser PDF preview |
| OCR | Ngoài DoD; stretch only |
| Spreadsheet | CSV/XLSX, preview/commit, cap 5.000 dòng, idempotent |
| Line imports | Inbound ASN + Stocktake count |
| UI | VI/EN parity; shadcn composition; responsive desktop/mobile |

> [!IMPORTANT]
> Phase 46 là umbrella delivery. P43 giữ baseline regression. P44/P45 không bị xóa, nhưng scope chưa triển khai của hai phase được thực thi và nghiệm thu tại P46.
>
> Execution bắt buộc theo thứ tự gate: **P46A → P46B/P46C/P46D → P46E**. Chỉ P46E được đóng umbrella sau khi traceability P43–P45 đạt 100% có evidence.

## Child Phase Map

| Phase | Phạm vi | Gate |
|---|---|---|
| [P46A](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46a_secure_attachment_content_preview.md) | Auth content API · sửa `/uploads` 404 · preview/download · pending cleanup · QC compat. | ✅ Done — tái xác nhận `rp4` + `rp5` 2026-07-27; strict gate 21/21, Release build sạch, browser UAT + video pass |

| [P46B](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46b_thumbnail_full_attachment_coverage.md) | Thumbnail lifecycle + 6 extended handlers/UI | ✅ **Hoàn thành** — remediation `rp4` + `rp5` PASS; strict automated gates và browser evidence đầy đủ |
| [P46C](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46c_master_spreadsheet_full_ops_exports.md) | Master IE 4 regression + Ops export 12 | ✅ **Hoàn thành** — strict verifier PASS; 76/76 integration tests; Master RBAC + 12 Ops export UI/Backend |
| [P46D](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46d_package_operational_line_imports.md) | Package IE + Inbound/Stocktake line imports | Batch ownership/TTL/concurrency/idempotency pass |
| [P46E](file:///d:/1_Project/48_Nexustock/planning/phases/phase_46e_rf_full_acceptance.md) | RF camera + DBM + zero-gap acceptance | P43/P44/P45 traceability 100% có evidence |

---

## 1. Mục tiêu

Biến upload thuần thành luồng tài liệu production hoàn chỉnh:

`upload → bind → list → thumbnail → preview ảnh/PDF → download → delete`

Đồng thời đóng toàn bộ gap attachment, ops export, Package spreadsheet, line import và RF camera còn lại từ P43–P45.

---

## 2. Root Cause Đã Xác Minh

- Backend sinh `PublicUrl` tương đối `/uploads/{tenant}/{key}`.
- Frontend hiện render trực tiếp `<a href={item.url}>`.
- Browser dùng frontend origin `http://localhost:3003`, tạo URL sai `http://localhost:3003/uploads/...`.
- Static files thực tế do backend phục vụ; Next.js trả `404`.
- Static URL cũng không bảo đảm tenant/RBAC và không chạy thống nhất qua Local/S3/R2/Azure/GCS.

### Fix bắt buộc

Không nối cứng cổng backend. UI chuyển sang content API theo attachment ID; backend xác thực quyền, tenant, attachment active và stream từ đúng storage provider.

---

## 3. Phạm vi

### In scope

1. Secure preview/download content API.
2. Preview dialog ảnh/PDF và attachment panel mới.
3. Thumbnail pipeline + lifecycle migrate/delete/backfill.
4. Extended attachment: Lot, Exception, LPN, Wave, Putaway, Cross-dock.
5. Ops export đủ 12 types P43–P44.
6. Package import/export CSV/XLSX.
7. Inbound ASN line preview/commit.
8. Stocktake count line preview/commit.
9. RF camera/file upload theo entity context.
10. Automated verification + browser DBM + evidence.

### Out of scope

- Full OCR engine.
- Antivirus scanning.
- DMS versioning/check-in/check-out.
- Multi-page PDF thumbnail.
- Offline blob queue mới nếu nền offline hiện tại không hỗ trợ an toàn.

### Non-negotiable

- Tenant isolation và permission trước khi stream.
- Không nhận storage key từ client để đọc file.
- File ≤10 MB; import/export ≤5.000 rows.
- Không phá P41–P43 và storage migration P42.
- JSON DTO camelCase.

---

## 4. Gap Ownership

| Nhóm | Deliverable | Owner P46 |
|---|---|---|
| P43 regression | PRODUCT/QC/INBOUND/SHIPMENT/STOCKTAKE/RMA attachments; Master IE 4; Ops export 4 | EP1–EP2, EP5, EP7 |
| P44 attachment | LOT/EXCEPTION/LPN/WAVE/PUTAWAY_PROPOSAL/CROSS_DOCK_CANDIDATE | EP4 |
| P44 export | LOTS/EXCEPTIONS/LPNS/INVENTORY_BALANCES/WAVES/PUTAWAY/CROSS_DOCK/REPLENISHMENT | EP5 |
| P45 Package | PACKAGES CSV/XLSX | EP6 |
| P45 lines | Inbound ASN + Stocktake count | EP6 |
| P45 field | RF camera/file fallback | EP7 |
| P45 polish | Thumbnail + provider-safe content URL | EP1–EP3 |

---

## 5. EP0 — SoT, Contract Freeze, Baseline

### Deliverables

- Phase file này là SoT.
- Thêm P46 vào `planning/IMPLEMENTATION_PLAN.md`.
- Tạo `planning/evidence/phase_46/baseline_disk_freeze.json` khi bắt đầu execute.
- Freeze route, DTO, entity types, provider implementations, UI panel locations, spreadsheet cases.
- Xác minh entity/table thật trước handler và query export.

### Gate

- Upstream P41–P43 regression xanh.
- Không còn câu hỏi API/DB contract chặn EP1.

---

## 6. EP1 — Secure Attachment Content Foundation

### Backend

#### `AttachmentService`

- Truy vấn attachment active theo ID trong tenant hiện tại.
- Soft-deleted attachment trả 404.
- Không expose storage key trong contract UI mới.

#### `AttachmentContentService` **NEW**

- Resolve provider theo attachment provider + tenant storage settings.
- `OpenReadAsync(storageKey)` và trả stream/contentType/fileName.
- Inline chỉ image/PDF; CSV/XLSX bắt buộc attachment.
- Log view/download theo attachment ID, entity type, provider.

#### `FilesController`

```http
GET /api/files/attachments/{id}/content?disposition=inline
GET /api/files/attachments/{id}/content?disposition=attachment
```

- Permission `files.read`.
- Tenant filter.
- `Content-Disposition` an toàn.
- `X-Content-Type-Options: nosniff`.
- `Cache-Control: private`.
- Range processing cho PDF nếu provider stream hỗ trợ.

### DTO

```json
{
  "id": "…",
  "fileName": "seal.jpg",
  "contentType": "image/jpeg",
  "sizeBytes": 204800,
  "previewKind": "image",
  "contentUrl": "/api/files/attachments/{id}/content",
  "thumbnailUrl": null
}
```

Giữ `url` tạm để tương thích migration; UI mới không dùng.

### Errors

| Code | HTTP |
|---|---:|
| `ATTACHMENT_NOT_FOUND` | 404 |
| `ATTACHMENT_DISPOSITION_INVALID` | 400 |
| `STORAGE_PROVIDER_ERROR` | 503 |

---

## 7. EP2 — Attachment Panel + Preview Dialog

### `attachment-preview-dialog.tsx` **NEW**

- shadcn `Dialog`, title/description đầy đủ accessibility.
- Fetch blob qua API client có token.
- Ảnh: fit/100%, object-contain, alt từ file name.
- PDF: object URL trong iframe/object; fallback Download.
- Skeleton/Spinner, Alert lỗi.
- Revoke object URL khi đổi file, đóng dialog, unmount.

### `entity-attachments-panel.tsx`

- Bỏ anchor trực tiếp `item.url`.
- Thumbnail/icon + file name + size + type + provider + timestamp.
- Actions: Preview, Download, Delete.
- Delete qua AlertDialog xác nhận.
- Reset input sau upload để chọn lại cùng file.
- Responsive desktop/mobile; keyboard/focus đúng.
- Dùng shadcn primitives, semantic tokens, `gap-*`; không `space-y-*`.

### i18n

Cập nhật parity:

- `frontend/messages/vi/Common.json`
- `frontend/messages/en/Common.json`

Keys: preview, download, metadata, confirm delete, unsupported preview, load content failed.

---

## 8. EP3 — Thumbnail Pipeline

### Database

Migration nullable, backward-compatible theo EF/PostgreSQL naming hiện hành:

```sql
ALTER TABLE files.file_attachments
  ADD COLUMN "ThumbnailKey" varchar(512) NULL,
  ADD COLUMN "ObjectsPurgedAt" timestamp with time zone NULL;
ALTER TABLE files.file_pending_uploads
  ADD COLUMN "ThumbnailKey" varchar(512) NULL;
```

`ObjectsPurgedAt` là durable marker: chỉ set sau khi original và thumbnail required đã delete hoặc NotFound. Down migration drop đúng ba cột sau khi generation/backfill đã tắt và purge backlog bằng 0.

### `ThumbnailService` **NEW**

- Magic-byte allowlist JPG/PNG/WebP; MIME/extension không phải nguồn quyết định.
- Identify trước decode; chặn >12.000 px mỗi chiều hoặc >40 MP; timeout 10 giây và cancellation xuyên suốt.
- Auto-orient, resize Max 256 không upscale, strip EXIF/IPTC/XMP/ICC.
- Output cố định JPEG quality 82, `image/jpeg`; key `{originalStorageKey}.thumb.jpg`.
- Thumbnail failure chỉ warning, không fail upload gốc; không log key/path/signed URL.

### Lifecycle

- Pending upload sở hữu original + thumbnail trước bind; attachment nhận ownership trong cùng commit bind.
- Pending TTL chỉ mark `PURGED` khi cả hai object đã delete/NotFound; lỗi giữ `PENDING` để retry.
- Soft delete attachment thử purge ngay; worker durable retry các row `DeletedAt != null && ObjectsPurgedAt == null`.
- Storage migrate copy + verify đủ original/thumbnail required trước provider cutover; source purge xử lý cả hai.
- Backfill delay 45 giây, batch 50, deterministic key, conditional update, race-safe đa instance.
- Rollback tắt generation/backfill nhưng giữ purge worker tới khi backlog bằng 0.

### File validation

Magic-byte check tối thiểu cho JPG/PNG/PDF/XLSX; không chỉ tin extension/MIME client.

---

## 9. EP4 — Extended Ops Attachments

### Entity types

```text
LOT
EXCEPTION
LPN
WAVE
PUTAWAY_PROPOSAL
CROSS_DOCK_CANDIDATE
```

### Backend

- Thêm 6 existence handlers theo registry P43.
- Allowlist tổng sau P46: 12 entity types.
- Fake/cross-tenant ID trả 404.

### Frontend

| Màn | entityType |
|---|---|
| Lots | `LOT` |
| Exceptions | `EXCEPTION` |
| LPN | `LPN` |
| Wave detail | `WAVE` |
| Putaway selected proposal | `PUTAWAY_PROPOSAL` |
| Cross-dock detail | `CROSS_DOCK_CANDIDATE` |

Reuse detail pane hiện có; không tạo drawer mới nếu không cần.

---

## 10. EP5 — Full Ops Exports

### Types

Giữ 4 P43:

```text
INBOUND_ORDERS
SHIPMENTS
STOCKTAKES
RMA
```

Thêm 8 P44:

```text
LOTS
EXCEPTIONS
LPNS
INVENTORY_BALANCES
WAVES
PUTAWAY_PROPOSALS
CROSS_DOCK_CANDIDATES
REPLENISHMENT_TASKS
```

### Rules

- Permission `ops.export`.
- Tenant filter; cap 5.000.
- Query projection + `AsNoTracking`; không N+1.
- CSV neutralize formula prefix `=`, `+`, `-`, `@`.
- XLSX dùng cell type rõ, không tạo công thức từ user data.
- Shared `OpsExportButtons`; không copy logic qua pages.

---

## 11. EP6 — Package + Idempotent Line Imports

### Package IE

Type `PACKAGES`:

```text
productCode,packageName,barcode,uomCode,conversionFactor,errorMessage
```

- Preview validate Product/UOM/barcode/conversionFactor > 0.
- Commit idempotent theo batch.
- CSV/XLSX roundtrip.

### Inbound ASN line import

```http
POST /api/inbound/lines/import/preview
POST /api/inbound/lines/import/commit
```

Template v1:

```text
lineNo,sku,qty,lotNo,uomCode,errorMessage
```

- Preview không ghi dữ liệu.
- Validate order/SKU/UOM/qty/lot.
- Không ghi đè qty đã received.
- Commit transaction.
- Batch `COMMITTED` chặn recommit, trả 409.

### Stocktake count line import

```http
POST /api/stocktakes/{id}/lines/import/preview
POST /api/stocktakes/{id}/lines/import/commit
```

- Validate stocktake open.
- Validate location/item/lot.
- Count không âm.
- Chặn commit nếu trạng thái không còn cho phép.

### UI

- Import button ở Inbound và Stocktake detail.
- Preview table lỗi theo dòng.
- Download error CSV.
- Commit disabled khi còn lỗi.
- Reuse import batch hiện có nếu contract cho phép.

---

## 12. EP7 — RF Camera + Program Close

### `rf-camera-upload.tsx` **NEW**

```html
<input type="file" accept="image/*" capture="environment">
```

- File picker fallback.
- Local preview trước upload.
- Reuse upload/bind/content API.
- Validate context entity type; không tin raw URL param.
- Không báo thành công khi offline; hiển thị retry.
- Không thêm offline blob queue nếu nền hiện tại không hỗ trợ an toàn.

### Mobile contexts

- Inbound receive.
- Shipment.
- Exception.
- LPN.

### Close program

- Gap inventory P43–P45 = 0 open.
- P43–P45 liên kết sang P46.
- README/CHANGELOG chỉ cập nhật khi Phase 46 hoàn thành.
- Evidence ảnh/video tại `planning/evidence/phase_46_dbm/`.

---

## 13. Security Gates

- Metadata lookup và stream đều tenant-filtered.
- Không đọc bằng storage key client gửi.
- Không redirect mặc định sang `PublicUrl`.
- Sanitize filename: CR/LF, path separators, quotes.
- MIME allowlist + magic bytes.
- `nosniff`; inline chỉ image/PDF.
- Deleted attachment không preview/download.
- Provider errors không lộ credential/bucket/path.
- CSV formula injection neutralized.

---

## 14. Observability

Structured logs:

```text
files.attachment.view
files.attachment.download
files.thumbnail.generated
files.thumbnail.failed
ops.export.completed
ops.line_import.previewed
ops.line_import.committed
files.rf.uploaded
```

Fields: tenantId, attachmentId/batchId, entityType/type, provider, rowCount, durationMs, success. Không log URL ký, token hoặc path vật lý.

---

## 15. Test Plan

### Automated

Backend unit/integration:

1. Content image/PDF happy path.
2. Thiếu `files.read` trả 403.
3. Cross-tenant/deleted trả 404.
4. Invalid disposition 400.
5. Provider failure 503.
6. Thumbnail generate/non-image skip/delete/migrate.
7. 12 entity types bind + fake ID.
8. 12 ops export types CSV/XLSX.
9. CSV formula injection.
10. Package roundtrip.
11. Inbound + Stocktake preview/commit/recommit 409.

Frontend:

- Lint/typecheck.
- VI/EN key parity.
- Object URL cleanup.
- Preview fallback.

Scripts:

```text
tests/verify_attachment_content_p46.ps1
tests/verify_extended_ops_p46.ps1
tests/verify_line_import_rf_p46.ps1
```

Regression:

- Phase 41 files/spreadsheet.
- Phase 42 storage migration.
- Phase 43 ops attachments/export.

### Browser DBM

1. Upload PNG ở Inbound; thumbnail và preview pass.
2. Upload PDF; inline preview và download pass.
3. Refresh; preview/download vẫn pass.
4. Delete; content endpoint trả 404.
5. Smoke 6 extended attachment screens.
6. Export Inventory Balances XLSX + Exceptions CSV.
7. Package preview/commit/export roundtrip.
8. Inbound line preview error + commit + recommit blocked.
9. Stocktake count import.
10. Mobile viewport camera/file fallback.
11. Console/network: 0 page error, 0 `MISSING_MESSAGE`, 0 UI request trực tiếp `/uploads`.
12. Lưu ảnh/video/walkthrough evidence.

---

## 16. Definition of Done

- [ ] PNG/JPEG/WebP preview pass.
- [ ] PDF preview pass.
- [ ] Mọi file hợp lệ download được.
- [ ] UI không điều hướng trực tiếp `/uploads/...`.
- [ ] Local + fake/object provider content test pass.
- [ ] Tenant/RBAC/delete gates pass.
- [ ] Thumbnail lifecycle pass.
- [ ] 12 attachment entity types pass.
- [x] 12 ops export types CSV/XLSX pass — P46C strict verifier, 76/76 integration tests.
- [ ] Package IE roundtrip pass.
- [ ] Inbound + Stocktake line import idempotent pass.
- [ ] RF camera/file fallback pass.
- [ ] VI/EN parity, lint/typecheck/regression pass.
- [ ] DBM có ảnh/video, console 0 lỗi.
- [ ] Gap inventory P43–P45 còn 0 mục mở.
- [ ] Master plan cập nhật Phase 46 hoàn thành.

---

## 17. Rollback

1. Tắt preview/thumbnail/RF feature flags; panel còn metadata + download API.
2. Revert UI routes; giữ content endpoint vì backward-compatible và an toàn hơn static URL.
3. Revert handlers/export/import theo EP độc lập.
4. Down migration chỉ drop `thumbnail_key`.
5. Purge orphan thumbnail bằng audited batch job.
6. Không xóa attachment gốc, import batch audit hoặc lịch sử export.

---

## 18. Ước lượng và thứ tự

| EP | Nội dung | Thời lượng |
|---|---|---:|
| EP0 | SoT/freeze | 0.5 ngày |
| EP1 | Secure content | 2 ngày |
| EP2 | Preview UI | 2 ngày |
| EP3 | Thumbnail | 2–3 ngày |
| EP4 | Extended attachments | 2–3 ngày |
| EP5 | Full exports | 1.5–2 ngày |
| EP6 | Package + 2 line imports | 3–4 ngày |
| EP7 | RF + verify/docs/dbm | 2 ngày |
| **Tổng** |  | **14–18 ngày** |

Critical path:

```text
EP0 → EP1 → EP2 → EP3 → EP7
```

EP4/EP5 bắt đầu sau EP1. EP6 có thể phát triển sau EP0 nhưng merge sau content foundation ổn định.

---

## 19. Readiness / Approval

| Vai trò | Kết luận | Ngày |
|---|---|---|
| JARVIS | Umbrella **100% Scope Mapped**; P46A/P46B/P46C Done; zero-gap gate vẫn khóa tại P46E | 2026-07-28 |
| FOUNDER | ☑ P46A Done · ☑ P46B Done · ☑ P46C Done · ☐ Proceed P46D · ☐ Hold | 2026-07-28 |

Điểm phải resolve trong EP0 từng child phase: dependency xử lý ảnh, entity/table path thật, permission name thật và mapping cột từ schema thật. Đây là verification trước code, không phải scope mở.

---

## 20. Liên kết

- [Phase 43](file:///d:/1_Project/48_Nexustock/planning/phases/phase_43_ops_attachments_spreadsheet.md)
- [Phase 44](file:///d:/1_Project/48_Nexustock/planning/phases/phase_44_extended_ops_attachments_exports.md)
- [Phase 45](file:///d:/1_Project/48_Nexustock/planning/phases/phase_45_line_import_rf_package_thumb.md)
- [Master plan](file:///d:/1_Project/48_Nexustock/planning/IMPLEMENTATION_PLAN.md)
