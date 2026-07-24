# PHASE 46B: Thumbnail Lifecycle + Full Attachment Coverage

## Execution Spec

| Mục | Giá trị |
|---|---|
| Trạng thái | ⬜ Spec Ready |
| Ước lượng | 3–4 dev-days |
| Upstream | P46A |
| Downstream | P46E |
| Scope nguồn | P44 attachment 6 types + P45 thumbnail |

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

## 3. Database

Migration nullable, backward-compatible:

```sql
ALTER TABLE file_attachments ADD COLUMN thumbnail_key varchar(512) NULL;
```

Down migration chỉ drop `thumbnail_key`. Không rewrite attachment cũ.

## 4. Thumbnail Service

- Magic-byte validate JPG/PNG/WebP trước decode.
- Max edge 256 px, giữ aspect ratio, auto-orient EXIF.
- Strip metadata nhạy cảm.
- Output WebP hoặc JPEG theo thư viện ảnh đã được repository chấp nhận tại EP0.
- Thumbnail failure log warning; object gốc vẫn thành công.
- Key deterministic từ original key + suffix để retry idempotent.
- Không tạo thumb cho PDF/CSV/XLSX.

### Lifecycle

| Operation | Original | Thumbnail |
|---|---|---|
| Upload | Put | Generate + Put |
| Delete | Delete/soft lifecycle hiện có | Delete |
| Storage copy | Copy | Copy hoặc regenerate |
| Purge source | Purge | Purge |
| Bind fail/TTL | Cleanup | Cleanup |
| Backfill | Không đổi | Generate batch-limited |

Backfill không block startup; retry có giới hạn; audit count success/fail.

## 5. Thumbnail API

```http
GET /api/files/attachments/{id:guid}/thumbnail
```

- `files.read`, tenant filter, active attachment.
- 404 khi chưa có thumb; UI fallback icon, không báo page error.
- Private cache + ETag nếu contract hiện có hỗ trợ.
- Không expose `thumbnail_key`.

## 6. Existence Handlers

Mỗi handler phải:

- Query entity thật trong tenant hiện tại.
- `AsNoTracking`, projection/Any.
- Không dựa vào ID client mà bỏ tenant filter.
- Fake ID và cross-tenant ID trả false.

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
| Lots | Selected lot/detail |
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

## 9. Tests

- 12 entity types: real/fake/cross-tenant bind.
- 3 image formats generate đúng max edge.
- EXIF rotate, corrupt image, huge dimensions.
- PDF/non-image skip.
- Original upload vẫn pass khi thumb fail.
- Delete/copy/purge/TTL cleanup không orphan.
- Backfill retry/idempotency.
- 6 extended screens: upload, thumbnail, preview, download, delete.

## 10. Definition of Done

- [ ] 12/12 attachment entity types pass.
- [ ] 6/6 extended UI contexts pass.
- [ ] Thumbnail generate/fallback/backfill pass.
- [ ] Local + fake cloud provider lifecycle pass.
- [ ] Không orphan object/thumb sau failure paths.
- [ ] Security/performance tests pass.
- [ ] `tests/verify_attachment_coverage_p46b.ps1` pass.

## 11. Rollback

Tắt thumbnail; UI fallback icon. Revert 6 handlers/pages độc lập nếu cần. Down migration drop nullable column; audited cleanup purge orphan thumb, không chạm original.
