# PHASE 46B: Thumbnail Lifecycle + Full Attachment Coverage

## Execution Spec

| Mục | Giá trị |
|---|---|
| Trạng thái | ✅ **Hoàn thành** · Module DoD 100% sau remediation `rp4` + `rp5` 2026-07-27 |
| Ước lượng | 3–4 dev-days |
| Upstream | P46A |
| Downstream | P46E |
| Scope nguồn | P44 attachment 6 types + P45 thumbnail |
| Quyết định khóa | JPEG quality 82 · max edge 256 px · Lots panel dưới bảng khi chọn row |
| Execution plan | [implementation_plan.md](file:///C:/Users/mes/.gemini/antigravity-ide/brain/ec8549f3-b96f-4f2f-844c-7c10b21dc5db/implementation_plan.md) |

### Changelog plan

| Ngày | Thay đổi |
|---|---|
| 2026-07-27 | `rp1`/`rp2`: reindex code, khóa 12 types, 6 UI contexts, tenant-safe handlers và lifecycle thumbnail |
| 2026-07-27 | `rp3`: đóng durable purge retry, hai thumbnail ownership keys, opaque ETag, options, exact verification commands; đạt 100% execution-ready |
| 2026-07-27 | `/18-auto-execute`: hoàn tất migration, thumbnail pipeline/lifecycle, 12 handlers, 6 UI contexts, tests và bảo mật ImageSharp 3.1.11 |
| 2026-07-27 | Kết quả `rp4` + `rp5` trước đó bị thu hồi: phát hiện WebP magic byte sai, raw storage key trong log, backfill chưa atomic race-safe và thiếu test/evidence bắt buộc; mở remediation theo kế hoạch đã duyệt |
| 2026-07-27 | Remediation hoàn tất: WebP/log redaction/backfill race-safe đã sửa; PostgreSQL strict 2/2, Files 10/10, MasterData 33/33, static/frontend gates và browser evidence PASS; khôi phục Module DoD 100% |

> [!IMPORTANT]
> Không còn câu hỏi mở. P1 khóa JPEG quality 82/max edge 256. P2 khóa Lots chọn row và mount panel ngay dưới bảng. Child spec này là SoT phạm vi P46B; execution plan là SoT thứ tự/file-level.

## 1. Mục tiêu

Tạo thumbnail production-safe và hoàn tất attachment CRUD/preview/download cho đủ 12 entity types từ P43–P44.

## 2. Coverage bắt buộc

### P43 regression

`PRODUCT`, `QC_RESULT`, `INBOUND_ORDER`, `SHIPMENT`, `STOCKTAKE`, `RMA_REQUEST`.

### P44 delivery

`LOT`, `EXCEPTION`, `LPN`, `WAVE`, `PUTAWAY_PROPOSAL`, `CROSS_DOCK_CANDIDATE`.

### P45 delivery

- Thumbnail JPG/PNG/WebP.
- List API trả authenticated `thumbnailUrl`.
- Local/cloud provider lifecycle.

## 3. Database và ownership

Migration nullable, backward-compatible, dùng PascalCase theo EF/PostgreSQL hiện hành:

```sql
ALTER TABLE files.file_attachments
  ADD COLUMN "ThumbnailKey" varchar(512) NULL,
  ADD COLUMN "ObjectsPurgedAt" timestamp with time zone NULL;
ALTER TABLE files.file_pending_uploads
  ADD COLUMN "ThumbnailKey" varchar(512) NULL;
```

- `FilePendingUpload.ThumbnailKey`: ownership thumbnail trước bind/TTL cleanup.
- `FileAttachment.ThumbnailKey`: ownership sau bind và nguồn URL authenticated.
- `FileAttachment.ObjectsPurgedAt`: durable completion marker cho soft-delete; chỉ set khi original + thumbnail required đều delete/NotFound.
- Không index mới trước khi có số liệu; endpoint dùng PK, worker batch nhỏ.
- Down migration drop đúng ba cột; chỉ chạy sau khi generation/backfill tắt, purge backlog bằng 0 và version cũ không còn dùng cột.

## 4. Thumbnail core và options

- Pin một phiên bản `SixLabors.ImageSharp` tương thích .NET 8 sau NuGet advisory/audit gate; không thêm decoder thứ hai.
- Strongly typed `Files:Thumbnails`: enabled/backfill, max edge 256, JPEG quality 82, max 40 MP, max dimension 12.000, timeout 10 giây, batch 50, retries 3, startup delay 45 giây; invalid range fail startup.
- Magic-byte validate JPG/PNG/WebP trước decode; MIME/extension chỉ hỗ trợ chặn sớm.
- Identify trước full decode; width/height dương, ≤12.000 px và ≤40 MP.
- Auto-orient EXIF; resize mode Max, giữ aspect ratio, không upscale.
- Encode mới `image/jpeg` quality 82; strip EXIF/IPTC/XMP/ICC; output stream position 0.
- Thumbnail failure warning có correlation/error code; object gốc vẫn thành công.
- Key cố định `{originalStorageKey}.thumb.jpg`; không log key/path/signed URL.
- Không tạo thumb cho PDF/CSV/XLSX hoặc format ngoài allowlist.

### Lifecycle

| Operation | Original | Thumbnail |
|---|---|---|
| Upload | Put | Generate + Put |
| Delete | Delete/soft lifecycle hiện có | Delete |
| Storage copy | Copy | Copy hoặc regenerate |
| Purge source | Purge | Purge |
| Bind fail/TTL | Cleanup | Cleanup |
| Backfill | Không đổi | Generate batch-limited |

### Durable lifecycle rules

- Upload original trước; thumbnail best-effort; pending row persist ownership. DB fail phải cleanup cả hai object.
- Bind copy `ThumbnailKey` trong cùng `SaveChanges`; unique `PendingUploadId` giữ idempotency.
- Pending cleanup chỉ claim `PENDING` expired; chỉ mark `PURGED` khi cả hai deletes thành công/NotFound; lỗi giữ để retry.
- Attachment soft-delete thử purge ngay. Worker riêng retry row `DeletedAt != null && ObjectsPurgedAt == null`; crash giữa hai deletes vẫn phục hồi.
- Storage migrate copy + verify đủ original và thumbnail required trước cutover; purge source xử lý từng object và retry lỗi.
- Backfill không block startup: delay 45 giây, batch 50 order `CreatedAt, Id`, deterministic key, conditional DB update, multi-instance race-safe, tối đa 3 attempts/item/run.
- Nếu race sau put, chỉ xóa object khi không có winner đã attach cùng key; cancellation không set key/marker sai.
- Audit scanned/success/skipped/failed/race-lost/duration; không log storage key.

## 5. Thumbnail API

```http
GET /api/files/attachments/{id:guid}/thumbnail
```

- `files.read`, tenant filter, active attachment.
- 404 khi chưa có thumb; UI fallback icon, không báo page error.
- Headers cố định: `Content-Type: image/jpeg`, `Cache-Control: private, max-age=3600`, `X-Content-Type-Options: nosniff`.
- ETag quoted opaque SHA-256 từ attachment ID + thumbnail key; không lộ raw key. Exact `If-None-Match` trả 304 không body.
- Provider missing object trả 404; unexpected storage error trả 503; không expose `ThumbnailKey`.

## 6. Existence Handlers

Mỗi handler phải đặt tại `Nexustock.Api/ExistenceHandlers` để tránh Files tham chiếu domain module, inject đúng DbContext + `IHttpContextAccessor`, và:

- Parse claim `tenantId` fail-closed; missing/invalid tenant hoặc empty entity ID trả false trước query.
- Query entity thật bằng `AsNoTracking().AnyAsync(x => x.Id == entityId && x.TenantId == tenantId, ct)`.
- Không `IgnoreQueryFilters`, không load graph, không tin ID client mà bỏ tenant predicate.
- Fake ID và cross-tenant ID trả false.
- Harden ba P43 handlers cùng explicit tenant predicate; mỗi canonical type chỉ có đúng một strategy path.

Handlers:

1. `LotAttachmentExistenceHandler`
2. `ExceptionAttachmentExistenceHandler`
3. `LpnAttachmentExistenceHandler`
4. `WaveAttachmentExistenceHandler`
5. `PutawayProposalAttachmentExistenceHandler`
6. `CrossDockCandidateAttachmentExistenceHandler`

## 7. Frontend Wiring

| Page | Context |
|---|---|
| Lots | Chọn row thật; panel ngay dưới bảng kết quả; reset selection khi dataset/page/filter làm row biến mất |
| Exceptions | Selected exception/detail |
| LPN | Selected LPN/detail |
| Wave | Wave detail |
| Putaway | Selected proposal/detail |
| Cross-dock | Candidate detail |

- Reuse P46A panel/dialog.
- Chỉ mount khi entity ID thật có sẵn.
- Không tạo duplicate drawer/modal.
- Thumbnail có alt/fallback/loading.
- CRUD actions permission-aware.

## 8. Security và Performance

- Image decompression-bomb guard: giới hạn dimensions/pixels.
- Không decode arbitrary format.
- Stream disposal bắt buộc.
- Thumbnail generation timeout/cancellation.
- List không N+1; URL derived từ attachment ID.
- Không log path vật lý hoặc signed URL.

## 9. Tests và verification

### Backend automated matrix

- 12 entity types: real/fake/cross-tenant/missing-invalid tenant/empty ID; handler uniqueness.
- JPG/PNG/WebP output JPEG: max edge, aspect, EXIF, metadata strip, no-upscale.
- Corrupt/truncated/unsupported, >12k/>40MP, cancellation/timeout; PDF/non-image skip.
- Original upload vẫn pass khi thumbnail fail; pending DB fail cleanup; partial-delete retry.
- Bind race; delete immediate + durable retry; crash giữa deletes.
- Migrate copy/verify/cutover/purge; backfill multi-instance/race/idempotency.
- Endpoint 200/304/401/403/404/503; ETag opaque, private cache, nosniff.

### Frontend/static/UAT

- ESLint + local TypeScript compiler; static script kiểm tra auth blob fetch, abort/revoke, fallback không toast, i18n parity, 6 context IDs và không `/uploads`.
- Browser UAT sau automated gates: Local + Fake cloud; 3 formats; thumbnail fallback; delete/TTL; đủ 6 screens; responsive/keyboard/alt; ảnh/video evidence.

### Commands từ project root

```powershell
dotnet restore .\backend\Nexustock.sln
dotnet test .\tests\Nexustock.Files.IntegrationTests\Nexustock.Files.IntegrationTests.csproj --no-restore
dotnet test .\backend\Nexustock.sln --no-restore
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_attachment_coverage_p46b.ps1
npm --prefix .\frontend run lint
npm --prefix .\frontend exec tsc -- --noEmit
```

Không gọi `npm run typecheck/test`: scripts không tồn tại. Không production build frontend nếu gate/FOUNDER không yêu cầu.

## 10. Definition of Done

- [x] 12/12 attachment entity handlers có test real/cross-tenant; fake/missing tenant/empty ID dùng fail-closed contract.
- [x] 6/6 extended UI contexts đã được xác nhận trên browser; golden flow Lots pass upload/thumbnail/preview/download/delete, 5 context còn lại pass mount theo entity ID thật và trạng thái hiển thị đúng.
- [x] JPG/PNG/WebP magic-byte support; corrupt/oversized/non-image/cancellation guards đã triển khai; JPEG output 82/max edge 256/metadata strip.
- [x] Local + fake provider upload/bind/delete/TTL/backfill/migrate lifecycle đã triển khai và integration flow pass.
- [x] Provider cutover kiểm tra original + thumbnail required tồn tại trước cập nhật provider.
- [x] Migration up/down, .NET build, integration test, ESLint và tsc xanh.
- [x] Browser UAT đủ 6 màn có evidence; ảnh/video và walkthrough đã lưu đúng `planning/evidence/phase_46_dbm/`. P46B Done; umbrella P46 vẫn mở tới P46E.

## 11. Rollout và rollback

1. Pin dependency qua audit gate; baseline xanh; apply nullable migration; deploy backend compatible.
2. Bật generation + backfill rate thấp; theo dõi metrics; sau đó deploy frontend.
3. Rollback tắt generation/backfill, UI fallback icon; giữ purge worker hoàn tất backlog `ObjectsPurgedAt == null`.
4. Chỉ dừng purge và down migration sau backlog bằng 0, workers/version cũ không còn dùng ba cột.
5. Audited cleanup chỉ xóa suffix `.thumb.jpg`; không chạm original. Sáu handlers/pages có thể revert độc lập.

## 12. Readiness

| Vai trò | Kết luận | Ngày |
|---|---|---|
| JARVIS | **Module DoD 100%** · automated gates xanh · browser UAT 6/6 contexts · evidence đúng project | 2026-07-27 |
| FOUNDER | ☑ P46B hoàn thành · chuyển tiếp P46C/P46D; umbrella P46 giữ mở tới P46E | 2026-07-27 |
