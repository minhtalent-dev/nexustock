# Phase 46A: Secure Attachment Content + Preview

Trạng thái: `🔄 In Progress — FP remediation` (Lần cập nhật cuối: 2026-07-24)

## 1. Description Spec

| Mục | Giá trị |
|---|---|
| Trạng thái | ✅ Done (100% Implemented & Verified) |
| Ước lượng | 3–4 dev-days |
| Upstream | P41 · P42 · P43 |
| Downstream | P46B · P46E |
| Scope nguồn | P43 attachment core + P45 provider-safe URL + lỗi preview hiện tại |

## 1. Mục tiêu

Sửa dứt điểm link `/uploads/...` mở sai frontend origin; chuẩn hóa luồng `upload → bind → list → preview/download → delete` bằng API có JWT, tenant và quyền. Hoàn thiện shared attachment UI để mọi entity ở P43/P44 tái sử dụng.

## 2. Coverage bắt buộc từ P43/P45

| ID | Deliverable nguồn | Xử lý tại 46A |
|---|---|---|
| P43-A1 | Upload/bind/list/delete attachment | Regression + hardening |
| P43-A2 | PRODUCT, QC_RESULT, INBOUND_ORDER, SHIPMENT, STOCKTAKE, RMA_REQUEST | Regression đủ 6 types |
| P43-A3 | Pending upload trước entity creation | Bind sau create + orphan cleanup |
| P43-A4 | QC `attachmentRefs` compatibility | Dual-write/read compatibility khóa rõ |
| P45-34 | Local/cloud URL phục vụ file | Thay signed/public URL UI bằng authenticated content API |
| New | Ảnh/PDF chưa preview, link 404 | Preview dialog + download blob |

> [!IMPORTANT]
> Signed URL không bị bỏ quên. P46A **thay thế có chủ đích** bằng authenticated streaming API để dùng đồng nhất Local/S3/R2/Azure/GCS và bảo vệ tenant. Provider có thể dùng signed URL nội bộ sau này nhưng không expose làm contract UI.

## 3. API Contract

```http
GET /api/files/attachments/{id:guid}/content?disposition=inline
GET /api/files/attachments/{id:guid}/content?disposition=attachment
```

| Điều kiện | Kết quả |
|---|---|
| Không có `files.read` | 403 |
| Sai tenant/không tồn tại/đã xóa | 404 `ATTACHMENT_NOT_FOUND` |
| disposition khác inline/attachment | 400 `ATTACHMENT_DISPOSITION_INVALID` |
| Provider lỗi | 503 `STORAGE_PROVIDER_ERROR` |
| Inline CSV/XLSX | Ép attachment |

Headers: `Content-Disposition` sanitize, `X-Content-Type-Options: nosniff`, `Cache-Control: private`. PDF bật range processing khi stream seekable.

### Attachment DTO

```json
{
  "id": "guid",
  "entityType": "INBOUND_ORDER",
  "entityId": "guid",
  "fileName": "evidence.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 1024,
  "kind": "DOCUMENT",
  "provider": "LOCAL",
  "previewKind": "pdf",
  "contentUrl": "/api/files/attachments/{id}/content",
  "thumbnailUrl": null,
  "createdAt": "2026-07-24T00:00:00Z"
}
```

Contract JSON camelCase. `storageKey` không trả trong DTO UI mới. `url` giữ tạm trong compatibility DTO nếu code cũ còn dùng; UI mới cấm dùng.

## 4. Backend Work

### Attachment content

- `IAttachmentService.GetActiveAsync(id)` tenant-filtered.
- `AttachmentContentService.OpenAsync(id)` resolve provider theo row.Provider.
- Stream bằng `IObjectStorageProvider.OpenReadAsync`.
- Chỉ đọc storage key từ DB.
- Sanitize filename chống CR/LF, slash, quote.
- MIME allowlist; inline chỉ `image/jpeg`, `image/png`, `image/webp`, `application/pdf`.
- Structured logs `files.attachment.view/download`.

### Pending upload lifecycle

- Pending upload trả token/storage metadata đủ để bind sau create.
- Khi create entity thành công: bind toàn bộ pending items trong cùng orchestration flow.
- Bind một phần thất bại: báo rõ items thất bại, không giả thành công toàn phần.
- Cancel form hoặc hết TTL: audited cleanup job xóa unbound object.
- TTL khóa tại 24 giờ; cleanup theo batch, không xóa object đã bind.
- Không tạo DB attachment row trước khi entity tồn tại.

### QC compatibility

- Trong giai đoạn P46: attachment row là SoT mới.
- `attachmentRefs` string tiếp tục được cập nhật từ danh sách attachment còn active khi save QC để không phá consumer cũ.
- Read ưu tiên attachment rows; fallback `attachmentRefs` chỉ với legacy record chưa migration.
- Test migration/dual-write; chưa xóa cột hoặc consumer cũ tại P46.

## 5. Frontend Work

### Shared preview dialog

- Fetch blob qua API client có Bearer token.
- Ảnh: fit/100%, alt=fileName.
- PDF: object/iframe; fallback Download.
- Unsupported: metadata + Download.
- Abort request và revoke object URL khi đóng/đổi/unmount.
- Loading/error/empty states đầy đủ.

### Attachment panel

- Không render `<a href={item.url}>`.
- Thumbnail/icon, filename, MIME/size/provider/time.
- Preview, Download, Delete; delete có xác nhận.
- Reset file input sau upload.
- VI/EN parity; keyboard/focus; responsive.
- Pending items hiển thị trạng thái upload/bind/error/remove.

## 6. Permission Matrix

| Hành động | Permission |
|---|---|
| List/preview/download | `files.read` |
| Upload/bind | `files.upload` |
| Delete | `files.delete` |

UI ẩn/disable action theo permission; backend luôn enforce độc lập.

## 7. Test Matrix

- 6 P43 entity types: bind/list/preview/download/delete.
- PNG/JPG/WebP inline; PDF inline; CSV/XLSX download only.
- Same-origin bug: network không có request UI tới `/uploads/...`.
- Local provider + fake object provider.
- 403, cross-tenant 404, soft-delete 404, provider 503.
- Malicious filename/MIME mismatch.
- Pending bind success, partial failure, cancel, TTL cleanup.
- QC dual-write + legacy read fallback.
- Refresh page vẫn preview/download.
- Console: 0 `MISSING_MESSAGE`, 0 page error.

## 8. Definition of Done

- [ ] Lỗi frontend `/uploads` 404 được loại khỏi UI flow.
- [ ] Authenticated content API pass mọi security test.
- [ ] Shared image/PDF preview và download pass.
- [ ] 6 entity types P43 regression pass.
- [ ] Pending upload không tạo orphan quá TTL.
- [ ] QC compatibility pass.
- [ ] VI/EN, accessibility, object URL cleanup pass.
- [ ] Automated script `tests/verify_attachment_content_p46a.ps1` pass.

## 9. Rollback

Giữ upload/bind/list cũ; tắt preview dialog bằng feature flag; panel fallback metadata + authenticated download. Không quay lại anchor public `/uploads`.
