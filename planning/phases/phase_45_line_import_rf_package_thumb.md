# PHASE 45: Line Excel Import + RF Camera + Package IE + Thumbnail

## Execution Spec Maturity

| Mục | Giá trị |
|---|---|
| **Mức hiện tại** | **95% Ready** |
| **Option** | **B** — bulk line import ClosedXML + mobile camera → Files Hub + Package IE + image thumb |
| **Trạng thái** | ⏳ Spec Ready · chờ **P44 ĐÓNG** |
| **Dev-days** | **5–7** |
| **Upstream** | P43 · P44 (Files Hub ổn định) |
| **Program** | Đóng nốt ❌ #31–#34 → **toàn bộ ❌ inventory = 0** |

### Changelog

| Ngày | Thay đổi |
|---|---|
| 2026-07-23 | Tách từ OOS P43 — đóng đủ ❌ còn lại |

### ❌ Owner = P45 (bắt buộc đóng)

| # | Gap | Deliverable |
|---|---|---|
| 31 | Package IE | Import/Export `PACKAGES` csv\|xlsx (qua product hoặc hub) |
| 32 | ASN/Pick line Excel import | Preview/commit lines Inbound + (opt) Pick/Stocktake lines |
| 33 | Mobile RF camera upload | Mobile page upload → `/api/files/upload` + bind entity |
| 34 | Thumbnail / signed URL | Generate thumb image ≤ max edge; URL serve local/cloud |

> OCR **không** bắt buộc DoD — nếu phình thì đánh dấu polish optional trong EP cuối; thumbnail + signed/local URL **bắt buộc** để đóng ❌ #34 phần thumb/URL. OCR = stretch / skip có lý do trong DoD nếu >1d.

---

## 1. Mục tiêu

Đóng hết ❌ còn lại sau P43–44: bulk dòng Excel, capture ảnh RF, Package spreadsheet, thumbnail — **program gap ❌ hoàn tất**.

---

## 2. Phạm vi

### In scope
1. Package import/export (`productCode,packageName,barcode,uomCode,conversionFactor`)  
2. Inbound ASN **line** import xlsx (preview/commit) — tối thiểu P0  
3. Pick line import **hoặc** stocktake count line import — chọn **1** P0 + 1 P1 trong EP  
4. Mobile RF: camera/file → upload + bind (Inbound/Shipment/Exception/LPN theo context)  
5. Thumbnail JPEG/WebP cho image attachments; list API trả `thumbnailUrl`  
6. verify_p45 + dbm mobile + admin  
7. Cập nhật gap_inventory: mọi ❌ = Done  

### Non-negotiable
Idempotent line import · tenant · cap rows 5000 · file ≤10MB · không phá P43/44.

### Out of scope
Full OCR engine · virus scan · DMS versioning.

---

## 3. Readiness

- [ ] P43 · P44 ĐÓNG  
- [ ] FOUNDER Proceed P45  

---

## 4. Setup

| Path | Vai trò |
|---|---|
| MasterData ImportService | + PACKAGES |
| Inbound `LineImportService` **NEW** | ASN lines preview/commit |
| (Opt) Wave/Inventory line import | Pick hoặc stocktake lines |
| Mobile FE camera component | Capture + upload |
| Files `ThumbnailService` | Resize on upload image |
| FileAttachment DTO | + thumbnailUrl |
| `tests/verify_ops_attach_p45.ps1` | Gates |

---

## 5. Permissions

| Permission | Ghi chú |
|---|---|
| `master_data.import/export` | Packages |
| `inbound.lines.import` **NEW** | ASN line import |
| `files.upload` | Mobile RF (reuse) |

---

## 6. Database

**Có thể** migration nhẹ:

```sql
-- UP
ALTER TABLE file_attachments ADD COLUMN IF NOT EXISTS thumbnail_key varchar(512) NULL;
-- DOWN
ALTER TABLE file_attachments DROP COLUMN IF EXISTS thumbnail_key;
```

Line import: reuse `import_batches` pattern hoặc bảng `ops_line_import_batches` (TenantId, type, status, row counts) — khóa EP0 chọn **reuse ImportBatch** nếu type string cho phép, else bảng mới.

---

## 7. API

### Package
`/api/imports?type=PACKAGES` · `/api/exports?type=PACKAGES`

### Line import
`POST /api/inbound/lines/import/preview` (multipart xlsx)  
`POST /api/inbound/lines/import/commit` `{ batchId }`  

Template: `lineNo,sku,qty,lotNo,uomCode,errorMessage`

### Mobile upload
Reuse `POST /api/files/upload` + bind — không API mới bắt buộc.

### Thumbnail
Upload image → lưu object + thumb key; `GET attachments` trả `thumbnailUrl`.

```json
{
  "id": "…",
  "fileName": "seal.jpg",
  "url": "/uploads/…/seal.jpg",
  "thumbnailUrl": "/uploads/…/seal_thumb.webp",
  "contentType": "image/jpeg",
  "sizeBytes": 204800
}
```

---

## 8. UI

- Master import: type PACKAGES (+ export trên product packages nếu có UI; không thì hub import đủ).  
- Inbound detail: **Import lines** button.  
- Mobile: nút Camera trên receive/exception/lpn flows.  
- Admin attachment list: hiện thumb.

---

## 9. Flow (line import pseudo)

```csharp
await using var tx = await _db.BeginTransactionAsync(ct);
var batch = await CreateBatchAsync("INBOUND_LINES", rows, ct);
foreach (var row in rows) {
  if (!TryParse(row, out var line, out var err)) { MarkError(batch, row, err); continue; }
  if (!await OrderExists(line.OrderId)) { MarkError(...); continue; }
  // preview only — commit inserts InboundOrderLines
}
await _db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

Idempotency: commit 1 lần / batchId; status COMMITTED chặn recommit.

---

## 10. Business Rules

Line import không ghi đè qty đã received (reject row).  
RF bind đúng entity context.  
Thumb max edge 256px · chỉ image/*.

---

## 11. Exceptions

| Code | HTTP |
|---|---|
| `LINE_IMPORT_TOO_LARGE` | 400 |
| `LINE_IMPORT_ALREADY_COMMITTED` | 409 |
| `THUMB_UNSUPPORTED` | 400 (non-image) |

---

## 12. Observability

Log line import success/error rows · mobile upload count.

---

## 13. Test Plan

Package IE roundtrip.  
ASN line preview errors + commit.  
Mobile upload bind EXCEPTION.  
Thumb generated for jpeg.  
Regression P43/P44 verifies.

---

## 14. DoD

- [ ] ❌ #31–#34 đóng (OCR optional documented nếu skip)  
- [ ] Gap inventory program: **0 ❌ còn mở** (trừ N/A #14)  
- [ ] verify_p45 + dbm + plan row 45 ✅  
- [ ] Walkthrough program complete  

---

## 15. OOS

OCR đầy đủ · antivirus · multi-page PDF thumb.

---

## 16. Downstream

Pilot khách dùng evidence đầy đủ; reporting có thể dùng exports P43–44.

---

## 17. Rollback

```sql
ALTER TABLE file_attachments DROP COLUMN IF EXISTS thumbnail_key;
```
Revert line import endpoints; batches giữ audit.

---

## 18. Bảo trì

Template line versioned `v1` header row.

---

## 19. Critique → 95%

| # | Rủi ro | Xử lý |
|---|---|---|
| 1 | Pick vs stocktake line | P0 = Inbound lines; P1 một trong hai |
| 2 | Mobile camera browser API | `capture=environment` + file fallback |
| 3 | Cloud thumb | Generate sync trên upload; fail → null thumb không fail upload |
| 4 | OCR scope | Optional — không block DoD |

**Maturity:** **95% Ready**

| FOUNDER | ☐ Proceed sau P44 · ☐ Hold |
|---|---|

---

## 20. EP

| EP | Goal |
|---|---|
| EP0 | Package IE + migration thumb col |
| EP1 | Inbound line import |
| EP2 | Mobile RF camera bind |
| EP3 | Thumbnail pipeline |
| EP4 | (P1) Pick hoặc stocktake line import |
| EP5 | verify + gap close docs |

---

## 21. Liên kết

- [P43](file:///d:/1_Project/48_Nexustock/planning/phases/phase_43_ops_attachments_spreadsheet.md)  
- [P44](file:///d:/1_Project/48_Nexustock/planning/phases/phase_44_extended_ops_attachments_exports.md)
