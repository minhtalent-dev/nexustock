# PHASE 46E: RF Camera + Full P43–P45 Acceptance

## Execution Spec

| Mục | Giá trị |
|---|---|
| Trạng thái | ⬜ Spec Ready |
| Ước lượng | 2–3 dev-days |
| Upstream | P46A · P46B · P46C · P46D |
| Kết quả | Đóng umbrella P46 và toàn bộ gap P43–P45 |
| Scope nguồn | P45 RF capture + full regression/DBM/evidence |

## 1. Mục tiêu

Hoàn thiện camera/file upload trên RF/mobile và thực hiện acceptance tổng để chứng minh 100% scope P43–P45 đã được triển khai, không chỉ có trong plan.

## 2. RF Camera Component

```html
<input type="file" accept="image/*" capture="environment">
```

- Camera rear-facing khi browser hỗ trợ.
- File picker fallback desktop/mobile.
- Local preview trước upload.
- Hiển thị file name/size và remove/retry.
- Reuse P46A upload/bind/content API.
- Entity context allowlist; không tin raw URL parameter.
- Không báo thành công khi offline/request fail.
- Không tạo offline blob queue mới nếu nền hiện tại không hỗ trợ persistence an toàn.
- Permission denied/cancel không sinh error toast sai.
- File vượt 10 MB hoặc MIME sai bị chặn rõ.

## 3. RF Contexts

| Context | Entity |
|---|---|
| Inbound receive | `INBOUND_ORDER` |
| Shipment | `SHIPMENT` |
| Exception | `EXCEPTION` |
| LPN | `LPN` |

EP0 xác minh route/component thật; đặt action tại màn có entity ID đã resolve. Mobile viewport 360/390/430 px phải thao tác được.

## 4. Acceptance Traceability P43–P45

### P43 — Core

- [ ] Master IE: UOMS CSV/XLSX preview/commit/export/roundtrip.
- [ ] Master IE: WAREHOUSES CSV/XLSX preview/commit/export/roundtrip.
- [ ] Master IE: ZONES CSV/XLSX preview/commit/export/roundtrip.
- [ ] Master IE: REASONS CSV/XLSX preview/commit/export/roundtrip.
- [ ] Attachments: PRODUCT/QC_RESULT/INBOUND_ORDER/SHIPMENT/STOCKTAKE/RMA_REQUEST.
- [ ] QC dual-write/legacy fallback.
- [ ] Pending upload/bind/cleanup.
- [ ] Ops exports: INBOUND_ORDERS/SHIPMENTS/STOCKTAKES/RMA CSV/XLSX.
- [ ] Permission `ops.export` + `files.*` regression.

### P44 — Extended

- [ ] Attachment handlers/UI: LOT/EXCEPTION/LPN/WAVE/PUTAWAY_PROPOSAL/CROSS_DOCK_CANDIDATE.
- [ ] Exports: LOTS/EXCEPTIONS/LPNS/INVENTORY_BALANCES/WAVES/PUTAWAY_PROPOSALS/CROSS_DOCK_CANDIDATES/REPLENISHMENT_TASKS.
- [ ] 6 fake IDs và cross-tenant attempts bị chặn.

### P45 — Completion

- [ ] PACKAGES CSV/XLSX preview/commit/export/roundtrip.
- [ ] Inbound ASN line preview/commit/idempotency.
- [ ] Stocktake count line preview/commit/idempotency.
- [ ] RF camera + file fallback.
- [ ] Thumbnail generation/lifecycle/backfill.
- [ ] Provider-safe preview/download; không UI request `/uploads`.
- [ ] OCR được ghi rõ out-of-scope có chủ đích theo P45.

## 5. Automated Gate

Chạy và lưu output:

```text
tests/verify_attachment_content_p46a.ps1
tests/verify_attachment_coverage_p46b.ps1
tests/verify_spreadsheet_exports_p46c.ps1
tests/verify_package_line_imports_p46d.ps1
tests/verify_rf_acceptance_p46e.ps1
```

Bắt buộc:

- Backend unit/integration xanh.
- Frontend lint/typecheck xanh.
- VI/EN key parity xanh.
- P41–P43 regression scripts xanh.
- Migration up/down rehearsal xanh trên disposable DB.
- Không skipped test trừ external provider test có fake tương đương và lý do ghi evidence.

## 6. Browser DBM Matrix

1. PNG upload → thumbnail → preview → download → delete.
2. PDF upload → inline preview → download → delete.
3. Refresh; content vẫn hoạt động.
4. 6 P43 attachment contexts smoke.
5. 6 P44 attachment contexts smoke.
6. 4 Master IE types roundtrip sample.
7. 12 Ops exports tải và mở được.
8. Package IE roundtrip.
9. Inbound line invalid → error CSV → valid → commit → recommit blocked.
10. Stocktake line valid/invalid/state transition.
11. RF camera/file fallback ở 390 px.
12. Permission denied states.
13. Cross-tenant API attempts.
14. Console 0 page error, 0 `MISSING_MESSAGE`.
15. Network 0 direct `/uploads` từ UI mới.

## 7. Evidence Contract

Lưu tại `planning/evidence/phase_46_dbm/`:

- `acceptance_matrix.md`
- `automated_results.json`
- `network_console_summary.md`
- ảnh từng nhóm flow.
- video CRUD attachment + spreadsheet/import + RF.
- API status evidence cho 403/404/409/503.
- migration rehearsal output.

Walkthrough phải link evidence thật; không đánh dấu pass bằng mô tả suông.

## 8. Zero-Gap Close Rules

Umbrella P46 chỉ được `✅ Done` khi:

1. 46A–46E đều Done.
2. Mọi checkbox traceability có evidence.
3. Không `TODO`, `TBD`, “permission hiện có”, “xác minh sau” trong execution contract.
4. Gap inventory P43–P45 = 0 Open.
5. P43–P45 giữ lịch sử nhưng liên kết kết quả nghiệm thu P46E.
6. README/CHANGELOG cập nhật nội dung end-user, không lộ kỹ thuật nhạy cảm.
7. Master `IMPLEMENTATION_PLAN.md` cập nhật trạng thái và evidence.

## 9. Definition of Done

- [ ] RF camera/file fallback pass 4 contexts.
- [ ] Automated gate 100% pass.
- [ ] Browser DBM matrix 15/15 pass.
- [ ] Traceability P43 100% pass.
- [ ] Traceability P44 100% pass.
- [ ] Traceability P45 100% pass.
- [ ] Evidence đầy đủ ảnh/video/API/results.
- [ ] Gap inventory 0 Open.
- [ ] README/CHANGELOG/master plan cập nhật.

## 10. Rollback

RF UI có feature flag; fallback file picker giữ hoạt động. Acceptance phase không rollback dữ liệu. Nếu gate fail, P46 giữ trạng thái In Progress, child phase lỗi được reopen; tuyệt đối không đánh dấu P43–P45 fully accepted.
