# PHASE 46E: RF Camera + Full P43–P45 Acceptance

## Execution Spec

| Mục | Giá trị |
|---|---|
| **Mức sẵn sàng** | **Non-hardware acceptance hoàn tất (`rp4`/`rp5` 2026-07-30)** |
| **Trạng thái** | 🔄 **In Progress — Deferred Manual Hardware Acceptance** |
| **Ước lượng** | 2–3 dev-days |
| **Upstream** | P46A · P46B · P46C · P46D — đều đã đóng |
| **Kết quả** | 11/11 automated PASS; camera thật chưa được nghiệm thu |
| **Scope nguồn** | P45 RF capture + full regression/DBM/evidence |
| **Migration mới** | Không |
| **Dependency mới** | Không |
| **Port FE** | `http://localhost:3003` |

### Changelog plan

| Ngày | Thay đổi |
|---|---|
| 2026-07-29 | `rp1`: reindex RF/mobile, Files API/service, bốn entity context và strict verifier P46A–P46D; khóa execution plan 100% ready |
| 2026-07-29 | Khóa shared RF component, camera/file fallback, client/server validation, structured observability, acceptance aggregator và zero-gap evidence contract |
| 2026-07-29 | Xác nhận không tạo bốn route mobile mới; RF action chạy responsive tại bốn màn nghiệp vụ đã có entity ID thật |
| 2026-07-30 | `rp4`/`rp5`: strict verifier 11/11 PASS; P43–P45 acceptance matrix, network/console summary và migration declaration hoàn tất |
| 2026-07-30 | FOUNDER khóa camera capture, metadata và permission-denied là `Deferred Manual Acceptance`; không tuyên bố camera gate PASS hoặc DBM 15/15 |

> [!IMPORTANT]
> **Kết luận `rp1`: Phase 46E đạt 100% execution-ready.** Không cần migration DB, dependency mới, API upload mới hoặc offline blob queue. Production diff tập trung vào component attachment dùng chung, bind-source validation, observability, i18n, test/verifier và evidence acceptance.

---

## 1. Mục tiêu

Hoàn thiện camera/file upload trên RF/mobile bằng Files API hiện hữu, sau đó thực hiện acceptance tổng để chứng minh 100% scope P43–P45 đã được triển khai bằng evidence thật.

Phase 46E chỉ hoàn thành khi đồng thời đạt:

1. RF camera/file fallback hoạt động tại bốn context nghiệp vụ thật.
2. Automated gates P41/P43 và P46A–P46E pass.
3. Browser DBM matrix 15/15 pass.
4. Traceability P43–P45 không còn `Open` hoặc `Fail`.
5. Evidence ảnh/video/API/network/console/migration đầy đủ.
6. Umbrella Phase 46, master plan, README và CHANGELOG được đồng bộ.

---

## 2. Baseline đã xác minh

- `EntityAttachmentsPanel` đã gắn đúng bốn context: `INBOUND_ORDER`, `SHIPMENT`, `EXCEPTION`, `LPN`.
- Upload hiện có giới hạn 10 MB, allowlist extension/MIME, magic-byte validation và pending-upload TTL 24 giờ.
- Bind hiện có allowlist entity, kiểm tra entity tồn tại, tenant isolation và idempotency theo `uploadId`.
- Mobile shell đã theo dõi `navigator.onLine`; dự án không có persistence an toàn cho blob camera.
- P46A–P46D đã có verifier/evidence; P46E còn thiếu RF UX, acceptance aggregator, evidence tổng và close documentation.
- Shared attachment flow hiện đã có upload → bind → list → thumbnail → preview/download → delete.

---

## 3. Quyết định khóa

1. **Tích hợp tại component dùng chung**, không nhân bản logic tại bốn page. Context có entity ID đã resolve tự nhận RF action.
2. **Hai input tách biệt**:
   - Camera: `accept="image/*" capture="environment"`.
   - Fallback: file picker giữ allowlist `.jpg,.jpeg,.png,.webp,.pdf`.
3. **Preview cục bộ chỉ cho ảnh**, dùng object URL và bắt buộc revoke khi đổi file hoặc unmount.
4. **Upload chỉ bắt đầu sau xác nhận**; cancel hoặc permission denied không sinh error toast sai.
5. **Client chặn sớm** file trên 10 MB, loại không hợp lệ và trạng thái offline. Server vẫn là nguồn xác thực cuối.
6. **Retry giữ file đã chọn** khi request fail; remove xóa selection và preview. Thành công mới reset selection.
7. **Không tạo offline blob queue**. Offline hiển thị trạng thái rõ, khóa submit; online trở lại cho phép retry file còn trong memory phiên.
8. **Không thêm feature-flag framework mới**. Rollback RF action bằng prop mặc định bật; fallback picker luôn còn.
9. **Observability đặt sau bind thành công**. Event `files.rf.uploaded` chỉ phát khi source là `RF_CAMERA`; không log filename, storage key, URL hoặc nội dung.
10. **Không đóng P46 bằng automated pass đơn lẻ**. Bắt buộc DBM 15/15 và evidence contract đầy đủ.
11. **Không tạo bốn route `/mobile/*` mới**. Dùng bốn màn nghiệp vụ hiện có responsive 360/390/430 px, vì đây là nơi entity ID đã resolve và attachment panel đã tồn tại.
12. **Camera thật phải có evidence thiết bị/browser hỗ trợ**. Desktop automation chỉ đủ chứng minh fallback và DOM capture contract.

---

## 4. RF Upload UX dùng chung

### 4.1 [NEW] `frontend/src/features/files/rf-camera-upload.tsx`

Component quản lý state chọn file, preview, validation, online/offline, remove và retry.

#### Contract props tối thiểu

```ts
type AttachmentUploadSource = "RF_CAMERA" | "FILE_PICKER";

type RfCameraUploadProps = {
  disabled?: boolean;
  uploading: boolean;
  onUpload: (file: File, source: AttachmentUploadSource) => Promise<boolean>;
};
```

#### UI/behavior contract

- Camera input có `capture="environment"`; fallback input không có `capture`.
- Kiểm tra `file.size <= 10 * 1024 * 1024`.
- Camera chỉ nhận MIME ảnh; fallback theo allowlist attachment hiện hữu.
- Hiển thị filename, kích thước, thumbnail local và upload source.
- Có action Upload, Remove, Retry.
- Cancel/permission denied không báo lỗi.
- Offline có trạng thái rõ và disable Upload.
- Request fail giữ file và preview để retry.
- Upload + bind thành công mới reset input/file/preview.
- Object URL phải revoke khi đổi file, remove và unmount.
- Accessibility: label gắn input, focus-visible, `aria-live`, vùng chạm phù hợp viewport 360/390/430 px.
- Dùng `Button`, `Alert`, `Badge`, `Spinner` và semantic tokens đã có; không thêm dependency.

### 4.2 [NEW] `frontend/src/features/files/rf-camera-upload.self-test.ts`

Self-test logic thuần chạy bằng Node:

- File đúng 10 MB pass; lớn hơn 10 MB fail.
- Camera MIME không phải ảnh fail.
- Fallback extension/MIME hợp lệ pass.
- Double extension, extension/MIME sai hoặc loại ngoài allowlist fail.
- Source mapping chỉ nhận `RF_CAMERA` và `FILE_PICKER`.

### 4.3 [MODIFY] `frontend/src/features/files/entity-attachments-panel.tsx`

- Compose `RfCameraUpload` thay input raw hiện tại.
- Tách upload/bind handler nhận `source`.
- Giữ nguyên permission `files.read`, `files.upload`, `files.delete`.
- Giữ pending-upload behavior khi entity chưa tồn tại.
- Giữ refresh/list/preview/download/delete đã nghiệm thu P46A/P46B.
- Chỉ reset file sau upload + bind thành công.
- Fail cho phép retry, không báo thành công giả.
- Thêm `enableRfCapture?: boolean`, mặc định `true`; khi false vẫn giữ fallback picker.

### 4.4 [MODIFY] `frontend/src/features/files/api.ts`

- Bổ sung `AttachmentUploadSource`.
- Bind payload thêm `source?: AttachmentUploadSource`.
- JSON gửi API tiếp tục camelCase: `uploadId`, `entityType`, `entityId`, `source`.

---

## 5. Backend Source Validation + Observability

### 5.1 [MODIFY] `backend/modules/Nexustock.Modules.Files/Dtos/FileDtos.cs`

Mở rộng tương thích ngược:

```csharp
public record BindAttachmentRequest(
    Guid? UploadId,
    string EntityType,
    Guid EntityId,
    string? Source = null);
```

Caller/test cũ dùng constructor ba tham số tiếp tục hoạt động.

### 5.2 [MODIFY] `backend/modules/Nexustock.Modules.Files/Services/AttachmentService.cs`

- Allowlist source: `RF_CAMERA`, `FILE_PICKER`; null giữ compatibility.
- Chuẩn hóa source trước validation.
- Reject source lạ bằng domain error ổn định.
- Sau DB commit thành công, log có cấu trúc:
  - event `files.rf.uploaded` khi source là `RF_CAMERA`;
  - `attachmentId`, `entityType`, `entityId`, `provider`, `sizeBytes`.
- Không log filename, storage key, thumbnail key, path, URL hoặc content.
- Duplicate bind trả attachment cũ; không phát success event lần hai.
- Không thay đổi tenant filter, entity existence check hoặc pending-upload idempotency hiện hữu.

### 5.3 [MODIFY] `tests/Nexustock.MasterData.IntegrationTests/PendingUploadLifecycleTests.cs`

- Constructor cũ vẫn hoạt động.
- Source hợp lệ bind thành công.
- Source lạ bị chặn đúng domain error.
- Retry cùng `uploadId` không sinh attachment thứ hai.

### 5.4 [NEW] `tests/Nexustock.MasterData.IntegrationTests/RfAttachmentAcceptanceTests.cs`

Test category `Phase46E`:

- Bind RF vào đủ `INBOUND_ORDER`, `SHIPMENT`, `EXCEPTION`, `LPN` thật.
- Fake ID trả 404 cho từng context.
- Cross-tenant entity/upload bị chặn.
- Oversize, MIME mismatch và unsupported type giữ lỗi domain đúng.
- RBAC upload/read/delete regression qua HTTP khi fixture hỗ trợ.
- Source hợp lệ/không hợp lệ và duplicate retry được cover.

---

## 6. i18n + Bốn Context thật

### 6.1 [MODIFY] `frontend/messages/en/Common.json`
### 6.2 [MODIFY] `frontend/messages/vi/Common.json`

Thêm key parity trong `Common.files`:

- Take photo, choose file, selected file, size, upload, remove, retry.
- Offline, file too large, unsupported type, invalid camera image.
- Preview alt/status và upload source.

UI production giữ tiếng Anh theo convention dự án; VI catalog đầy đủ 1:1.

### 6.3 Context matrix

| Context | Entity type | Integration point | ID contract |
|---|---|---|---|
| Inbound receive | `INBOUND_ORDER` | `frontend/src/app/admin/inbound/[id]/receive/page.tsx` | `orderId` từ route đã resolve |
| Shipment | `SHIPMENT` | `frontend/src/app/admin/outbound/page.tsx` | `selectedShipment.id` |
| Exception | `EXCEPTION` | `frontend/src/app/admin/exceptions/page.tsx` | `selectedException.id` |
| LPN | `LPN` | `frontend/src/app/admin/lpn/page.tsx` | `selectedLpn.id` |

Không sửa page nếu reindex khi execute xác nhận shared component đã đủ. Static gate bắt buộc chứng minh cả bốn entity type đang gắn với ID thật.

> [!NOTE]
> Route `/mobile/lpn` là flow di chuyển pallet, không có attachment panel. P46E định nghĩa context nghiệp vụ, không bắt buộc tạo route mobile mới. RF camera được cung cấp responsive tại bốn màn đang sở hữu entity ID; viewport 360/390/430 px là acceptance gate.

---

## 7. Acceptance Traceability P43–P45

### 7.1 P43 — Core

- [x] Master IE: UOMS CSV/XLSX preview/commit/export/roundtrip — [acceptance](../evidence/phase_46_dbm/acceptance_matrix.md).
- [x] Master IE: WAREHOUSES CSV/XLSX preview/commit/export/roundtrip — [acceptance](../evidence/phase_46_dbm/acceptance_matrix.md).
- [x] Master IE: ZONES CSV/XLSX preview/commit/export/roundtrip — [acceptance](../evidence/phase_46_dbm/acceptance_matrix.md).
- [x] Master IE: REASONS CSV/XLSX preview/commit/export/roundtrip — [acceptance](../evidence/phase_46_dbm/acceptance_matrix.md).
- [x] Attachments: PRODUCT/QC_RESULT/INBOUND_ORDER/SHIPMENT/STOCKTAKE/RMA_REQUEST — [acceptance](../evidence/phase_46_dbm/acceptance_matrix.md).
- [x] QC dual-write/legacy fallback — G08 PASS.
- [x] Pending upload/bind/cleanup — G06/G08 PASS.
- [x] Ops exports: INBOUND_ORDERS/SHIPMENTS/STOCKTAKES/RMA CSV/XLSX — G10 PASS.
- [x] Permission `ops.export` + `files.*` regression — G07/G10 PASS.

### 7.2 P44 — Extended

- [x] Attachment handlers/UI: LOT/EXCEPTION/LPN/WAVE/PUTAWAY_PROPOSAL/CROSS_DOCK_CANDIDATE — G09 + browser evidence.
- [x] Exports: LOTS/EXCEPTIONS/LPNS/INVENTORY_BALANCES/WAVES/PUTAWAY_PROPOSALS/CROSS_DOCK_CANDIDATES/REPLENISHMENT_TASKS — G10 PASS.
- [x] Sáu fake IDs và cross-tenant attempts bị chặn — G06/G09 PASS.

### 7.3 P45 — Completion

- [x] PACKAGES CSV/XLSX preview/commit/export/roundtrip — G11 PASS.
- [x] Inbound ASN line preview/commit/idempotency — G11 PASS.
- [x] Stocktake count line preview/commit/idempotency — G11 PASS.
- [ ] RF camera + file fallback — file fallback/DOM contract PASS; camera thật `DEFERRED — MANUAL HARDWARE ACCEPTANCE`.
- [x] Thumbnail generation/lifecycle/backfill — G09 PASS.
- [x] Provider-safe preview/download; không UI request `/uploads` — G08/G09 + network summary.
- [x] OCR được ghi rõ out-of-scope có chủ đích theo P45.

Traceability chi tiết: [acceptance_matrix.md](../evidence/phase_46_dbm/acceptance_matrix.md).

---

## 8. Strict Verifier + Acceptance Aggregator

### 8.1 [NEW] `tests/verify_rf_acceptance_p46e.ps1`

Fail-fast gates:

1. Static RF contract: rear camera capture, fallback input, 10 MB check, object URL cleanup, online guard, retry/remove.
2. Bốn context/entity IDs và backend allowlists.
3. Không direct `/uploads`; không raw storage locator trong log.
4. EN/VI `Common.files` parity.
5. RF self-test.
6. Frontend TypeScript typecheck + ESLint.
7. Backend build.
8. `Phase46E` integration tests.
9. P41/P43 regression scripts.
10. P46A–P46D strict verifier regression.
11. Xuất machine-readable result và log từng gate.

Script contract:

- Root-safe; tính project root từ `$PSScriptRoot`.
- Khôi phục working directory trong `finally`.
- Exit code khác 0 khi bất kỳ gate fail.
- Không ghi pass giả nếu command bị skip hoặc output thiếu.
- Ghi thời gian, exit code, status và evidence path từng gate.
- Output JSON tại `planning/evidence/phase_46_dbm/automated_results.json`.

### 8.2 Existing verifier handling

| File | Action |
|---|---|
| `tests/verify_attachment_content_p46a.ps1` | Chỉ sửa static input-reset assertion nếu RF component thay ownership input; giữ security/content gates |
| `tests/verify_attachment_coverage_p46b.ps1` | Chạy regression, không sửa nếu pass độc lập |
| `tests/verify_spreadsheet_exports_p46c.ps1` | Chạy regression, không sửa nếu pass độc lập |
| `tests/verify_package_line_imports_p46d.ps1` | Chạy regression, không sửa nếu pass độc lập |
| `tests/verify_files_spreadsheet.ps1` | P41 regression |
| `tests/verify_ops_attach_p43.ps1` | P43 regression |

### 8.3 Lệnh tổng

```powershell
.\tests\verify_rf_acceptance_p46e.ps1
```

Bắt buộc bao phủ:

```text
RF self-test
Frontend TypeScript + ESLint
Backend build
Phase46E integration tests
P41/P43 regression
P46A/P46B/P46C/P46D strict gates
EN/VI parity
Static security/observability checks
```

---

## 9. Browser DBM Matrix

1. PNG upload → thumbnail → preview → download → delete.
2. PDF upload → inline preview → download → delete.
3. Refresh; content vẫn hoạt động.
4. Sáu P43 attachment contexts smoke.
5. Sáu P44 attachment contexts smoke.
6. Bốn Master IE types roundtrip sample.
7. Mười hai Ops exports tải và mở được.
8. Package IE roundtrip.
9. Inbound line invalid → error CSV → valid → commit → recommit blocked.
10. Stocktake line valid/invalid/state transition.
11. RF camera/file fallback tại 360/390/430 px.
12. Permission denied states.
13. Cross-tenant API attempts.
14. Console 0 page error, 0 `MISSING_MESSAGE`.
15. Network 0 direct `/uploads` từ UI mới.

### 9.1 RF negative-path matrix

- Camera cancel: không error toast, không upload request.
- Camera permission denied: fallback vẫn thao tác được.
- File đúng 10 MB: cho phép submit.
- File trên 10 MB: client chặn rõ; server guard giữ nguyên.
- MIME/extension sai: client chặn rõ; server validation vẫn cover.
- Offline: không success toast, không upload request.
- Request fail: file/preview còn, Retry hoạt động.
- Upload thành công nhưng bind fail: không báo saved; pending lifecycle tuân theo P46A.

> [!WARNING]
> Camera thật và permission-denied cần thiết bị/browser hỗ trợ. Browser desktop automation chỉ chứng minh file fallback và DOM `capture="environment"`. Nếu chưa có evidence camera thật, Phase 46E và umbrella Phase 46 phải giữ `In Progress`.

---

## 10. Evidence Contract

Lưu tại `planning/evidence/phase_46_dbm/`.

### 10.1 [NEW] `acceptance_matrix.md`

Mỗi requirement P43/P44/P45 gồm:

- ID/scope nguồn.
- Code/test owner.
- Automated result.
- Ảnh/video/API evidence.
- Trạng thái `Pass`, `Fail` hoặc `Open`.
- Ngoại lệ và phê duyệt nếu có.

Không đánh dấu Pass khi thiếu evidence path thật.

### 10.2 [NEW] `automated_results.json`

- Gate ID/name.
- Start/end time.
- Exit code.
- Pass/Fail/Skip.
- Log/evidence path.
- Skip reason; mặc định skip không được tính Pass.

### 10.3 [NEW] `network_console_summary.md`

- Console page error = 0.
- `MISSING_MESSAGE` = 0.
- Direct `/uploads` requests = 0.
- API status evidence cho 403/404/409/503.
- URL/route và timestamp.
- Che token, tenant/user và dữ liệu nhạy cảm.

### 10.4 [NEW] `migration_rehearsal.log`

- Ghi `NO_NEW_MIGRATION` cho P46E.
- Rehearse migration up/down hiện hành trên disposable DB theo script có sẵn.
- Không đụng staging hoặc production DB.

### 10.5 [UPDATE] `walkthrough.md`

- DBM matrix 15/15.
- Ảnh nhóm attachment, spreadsheet/import, permission/error và RF 360/390/430.
- Video tối thiểu: attachment CRUD; spreadsheet/import; RF camera/file fallback.
- Link automated JSON, network/console, API status và migration rehearsal.

Walkthrough phải link evidence thật; mô tả suông không đủ để đóng gate.

---

## 11. Execution Packages

### EP0 — Freeze Baseline + Gap Inventory

- Ghi git baseline, working-tree status và danh sách evidence hiện có.
- Reindex bốn page contexts, shared panel, Files API/service và P41–P46 gates.
- Tạo acceptance matrix với trạng thái ban đầu `Open`.
- Không kế thừa Pass từ plan hoặc mô tả không có evidence.

**Exit gate:** Baseline/gap inventory đầy đủ; không có context hoặc verifier chưa xác định owner.

### EP1 — RF Component + Pure Logic

- Tách validation helpers có self-test.
- Xây camera/fallback UI bằng component đã cài.
- Xử lý preview lifecycle, cancel, offline, retry/remove và accessibility.

**Exit gate:** RF self-test pass; object URL lifecycle và negative paths có coverage.

### EP2 — Bind Source + Observability

- Mở rộng DTO tương thích ngược.
- Validate source backend.
- Log thành công đúng semantics, không lộ storage locator.
- Bổ sung integration coverage source/idempotency/cross-tenant.

**Exit gate:** `Phase46E` backend tests pass; duplicate retry không phát event hai lần.

### EP3 — Context + i18n Integration

- Compose RF component vào shared attachment panel.
- Xác minh bốn context thật và viewport 360/390/430.
- Chạy parity/typecheck/lint.
- Chỉ sửa P46A verifier nếu static contract input ownership thay đổi.

**Exit gate:** EN/VI parity, typecheck, lint và bốn context static gate pass.

### EP4 — Acceptance Verifier

- Tạo P46E fail-fast aggregator.
- Chạy self-test, backend tests, frontend gates, P41/P43 và P46A–P46D.
- Xuất JSON/log.
- Gate fail phải reopen đúng child scope; không cập nhật Done.

**Exit gate:** Automated gate 100% pass; không skip không hợp lệ.

### EP5 — DBM + Evidence

- [x] Hoàn tất Browser DBM cho toàn bộ case không phụ thuộc camera hardware.
- [x] Kiểm thử file fallback, validation, network/console/cross-tenant bằng browser + automated evidence.
- [x] Lưu ảnh, video, acceptance matrix, network/console summary và migration declaration.
- [ ] Camera capture, metadata và permission-denied trên thiết bị thật — `DEFERRED — MANUAL HARDWARE ACCEPTANCE`.

**Exit gate:** Non-hardware acceptance hoàn tất; DBM 15/15 chưa đạt và không được tuyên bố cho tới manual hardware acceptance.

### EP6 — Zero-Gap Close

- Đối chiếu acceptance matrix: `Open = 0`, `Fail = 0`.
- Cập nhật P46E, umbrella P46, master plan, README và CHANGELOG.
- P44/P45 giữ lịch sử `Superseded`, thêm link acceptance P46E.
- Chỉ commit/đóng phase nếu toàn bộ contract đạt.

**Exit gate:** P43/P44/P45 traceability 100%; P46A–P46E Done; umbrella Phase 46 Done.

---

## 12. Zero-Gap Close Rules

Umbrella P46 chỉ được `✅ Done` khi:

1. P46A–P46E đều Done.
2. Mọi checkbox traceability có evidence thật.
3. Không còn `TODO`, `TBD`, “permission hiện có”, “xác minh sau” trong execution contract.
4. Gap inventory P43–P45 có `Open = 0`, `Fail = 0`.
5. P43–P45 giữ lịch sử nhưng liên kết kết quả P46E.
6. README/CHANGELOG cập nhật nội dung end-user, không lộ thông tin nhạy cảm.
7. Master `IMPLEMENTATION_PLAN.md` cập nhật trạng thái và evidence.
8. Browser DBM 15/15 pass.
9. Camera thật có evidence.
10. Không skipped test, trừ external provider có fake tương đương, lý do và phê duyệt ghi evidence.

---

## 13. Tài liệu đóng Phase

Chỉ cập nhật trạng thái Done sau EP6.

### 13.1 `planning/phases/phase_46e_rf_full_acceptance.md`

- Tick DoD/traceability theo evidence thật.
- Ghi 15/15 DBM, automated summary, gap inventory = 0.
- Chuyển trạng thái `✅ Done` khi đủ zero-gap rules.

### 13.2 `planning/phases/phase_46_attachment_experience_ops_spreadsheet_completion.md`

- Child map P46E Done.
- Umbrella Phase 46 Done.
- Link acceptance matrix/walkthrough.
- Ghi P43–P45 traceability 100%.

### 13.3 `planning/IMPLEMENTATION_PLAN.md`

- Cập nhật rows 43–46E và bảng tiến độ.
- P44/P45 giữ `Superseded`, thêm link acceptance P46E.
- Không thay đổi lịch sử child phase đã đóng.

### 13.4 `README.md` + `CHANGELOG.md`

- Nội dung end-user: chụp/tải bằng chứng ảnh trên thiết bị, cải thiện attachment, nhập/xuất dữ liệu vận hành.
- Không lộ storage internals, security controls, tenant identifiers hoặc stack kỹ thuật.
- Theo version rule cùng ngày: cập nhật version hiện tại, không tự nâng version.

---

## 14. Definition of Done

- [ ] RF camera/file fallback pass bốn contexts — file fallback/contract PASS; camera hardware Deferred.
- [ ] Camera thật có evidence — `DEFERRED — MANUAL HARDWARE ACCEPTANCE`.
- [ ] Cancel/deny/offline/oversize/bad MIME/retry pass — automated negative paths PASS; hardware deny Deferred.
- [x] Automated gate 100% pass — 11/11, không skip.
- [x] P41/P43 và P46A–P46D regression pass.
- [x] Frontend typecheck/lint và EN/VI parity pass.
- [x] Backend build + `Phase46E` integration tests pass.
- [ ] Browser DBM matrix 15/15 pass — 12 PASS, 1 PARTIAL, 2 Deferred.
- [x] Traceability P43 100% pass.
- [x] Traceability P44 100% pass.
- [ ] Traceability P45 — mọi non-hardware scope PASS; camera hardware Deferred.
- [x] Evidence ảnh/video/API/results/network/console/migration cho non-hardware scope đầy đủ.
- [x] Gap inventory `Fail = 0`, `Open không owner = 0`; hardware deferred = 3.
- [x] Master plan/umbrella cập nhật trạng thái trung thực; README/CHANGELOG chờ phase Done.
- [ ] P46E và umbrella Phase 46 chuyển `✅ Done` — chờ manual hardware acceptance.

---

## 15. Rollback

1. Tắt `enableRfCapture` tại composition; fallback picker vẫn hoạt động.
2. Revert source field/log riêng nếu backend regression; request cũ vẫn tương thích vì field optional.
3. Không có migration hoặc data rewrite cần rollback.
4. Acceptance phase không rollback dữ liệu P46A–P46D đã nghiệm thu.
5. Gate fail giữ P46/P46E `In Progress`; child phase lỗi được reopen đúng owner.
6. Không sửa evidence thành Pass thủ công và không đánh dấu P43–P45 fully accepted khi thiếu evidence.

---

## 16. Rủi ro + Guardrails

| Rủi ro | Guardrail | Gate |
|---|---|---|
| Browser không mở camera sau | `capture="environment"` + file fallback | DOM static + camera-device UAT |
| Cancel/deny bị báo lỗi giả | Không xử lý khi input không có file | RF negative-path DBM |
| Blob preview leak memory | Revoke object URL khi đổi/remove/unmount | Static + self-test/review |
| Upload offline báo thành công | Online guard + chỉ toast sau bind pass | Offline DBM |
| File lớn/MIME sai qua client | Client validation + server validation giữ nguyên | Self-test + integration |
| Retry tạo attachment trùng | Bind idempotency theo `uploadId` | Integration test |
| Client giả source RF | Backend source allowlist; source chỉ phục vụ UX/log, không cấp quyền | Integration test |
| Log lộ storage locator | Static redaction gate | P46B + P46E verifier |
| Evidence desktop giả camera thật | Camera-device evidence bắt buộc | Zero-gap close rule |
| Aggregator ghi pass khi skip | Skip mặc định không tính Pass | JSON/result validator |

---

## 17. Approval Baseline

Execution mặc định theo các điều kiện đã khóa:

- Không tạo bốn route mobile mới.
- Dùng bốn màn nghiệp vụ hiện có responsive 360/390/430 px.
- Không thêm dependency, migration, upload API hoặc offline blob queue.
- Cần thiết bị/browser có camera trước EP6 để đóng 100%.
- Nếu yêu cầu route `/mobile/*` riêng hoặc offline persistence, phải cập nhật spec và re-approve trước execution.
