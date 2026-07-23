# PHASE 44: Extended Ops Attachments + Full Ops Exports

## Execution Spec Maturity

| Mục | Giá trị |
|---|---|
| **Mức hiện tại** | **95% Ready** |
| **Option** | **B** — reuse panel + handler registry P43 |
| **Trạng thái** | ⏳ Spec Ready · chờ **P43 ĐÓNG** |
| **Dev-days** | **5–6** |
| **Upstream** | Phase **43** Core (handler + OpsExports scaffold) |
| **Downstream** | Phase **45** |

### Changelog

| Ngày | Thay đổi |
|---|---|
| 2026-07-23 | Tách từ P43 monolit — đóng ❌ #10,#12,#15,#16,#18,#20,#21,#22 |

### ❌ Owner = P44 (bắt buộc đóng)

| # | Module | Deliverable |
|---|---|---|
| 10 | Lot | Attach `LOT` + export `LOTS` |
| 12 | Putaway | Attach `PUTAWAY_PROPOSAL` + export `PUTAWAY_PROPOSALS` |
| 15 | Wave | Attach `WAVE` + export `WAVES` |
| 16 | Cross-dock | Attach `CROSS_DOCK_CANDIDATE` + export `CROSS_DOCK_CANDIDATES` |
| 18 | Inventory balances | Export `INVENTORY_BALANCES` |
| 20 | Exception | Attach `EXCEPTION` + export `EXCEPTIONS` |
| 21 | Replenishment | Export `REPLENISHMENT_TASKS` |
| 22 | LPN | Attach `LPN` + export `LPNS` |

---

## 1. Mục tiêu

Đóng toàn bộ ❌ attach/export **extended ops** còn lại sau P43 — bằng chứng Lot/Exception/LPN/Wave/Putaway/Cross-dock + export tồn & replenishment.

---

## 2. Phạm vi

### In scope
1. Handlers + allowlist: `LOT` · `EXCEPTION` · `LPN` · `WAVE` · `PUTAWAY_PROPOSAL` · `CROSS_DOCK_CANDIDATE`  
2. FE panels trên Lots · Exceptions · LPN · Wave detail · Putaway · Cross-dock detail  
3. Ops export types còn lại (8) + FE buttons  
4. `tests/verify_ops_attach_p44.ps1` + dbm  
5. Plan row 44 ✅  

### Out of scope → P45
Package IE · ASN/Pick line import · RF camera · Thumbnail/OCR.

### Non-negotiable
Reuse panel · không fork storage · regression P43 verify xanh.

---

## 3. Readiness

- [ ] Phase **43** Module DoD 100%  
- [ ] FOUNDER Proceed P44  

---

## 4. Setup

| Path | Vai trò |
|---|---|
| Handlers Lot/Exception/Lpn/Wave/Putaway/CrossDock | Exists checks |
| AttachmentService allowlist | +6 types |
| OpsExportsController | +8 types |
| FE pages tương ứng | Panels + Export |
| `tests/verify_ops_attach_p44.ps1` | Gates |

---

## 5. Permissions

Reuse `files.*` · `ops.export` (P43). Không permission mới bắt buộc.

---

## 6. Database

Không migration. Allowlist sau P44:

```text
(+ P43) | LOT | EXCEPTION | LPN | WAVE | PUTAWAY_PROPOSAL | CROSS_DOCK_CANDIDATE
```

| entityType | DbContext / bảng |
|---|---|
| LOT | Inventory / `Lots` |
| EXCEPTION | Exceptions / `operational_exceptions` |
| LPN | Lpn / `lpns` |
| WAVE | Wave / `PickingWaves` |
| PUTAWAY_PROPOSAL | Putaway / proposals |
| CROSS_DOCK_CANDIDATE | CrossDocking / candidates |

---

## 7. API

Attachments: cùng `/api/files/*`.  
Ops export thêm:

| type | Cột chính |
|---|---|
| LOTS | lotNo, itemCode, qcStatus |
| EXCEPTIONS | code, type, severity, status, reasonCode, createdAt |
| LPNS | lpnNo, status, locationCode, createdAt |
| INVENTORY_BALANCES | itemCode, lotNo, locationCode, qtyOnHand, qtyReserved, qtyAvailable |
| WAVES | waveNo, status, createdAt, taskCount |
| PUTAWAY_PROPOSALS | id, status, itemCode, createdAt |
| CROSS_DOCK_CANDIDATES | id, status, createdAt |
| REPLENISHMENT_TASKS | id, status, itemCode, createdAt |

Cap 5000 · csv\|xlsx.

---

## 8. UI

| Màn | entityType |
|---|---|
| Lots | `LOT` |
| Exceptions | `EXCEPTION` |
| LPN | `LPN` |
| Wave `[id]` | `WAVE` |
| Putaway | `PUTAWAY_PROPOSAL` |
| Cross-dock `[id]` | `CROSS_DOCK_CANDIDATE` |

Export buttons trên list tương ứng + Inventory page.

---

## 9. Flow

Pseudo: đăng ký 6 handlers vào `IEnumerable<IEntityExistenceHandler>` (pattern P43).

```csharp
// WAVE
return await _wave.PickingWaves.AnyAsync(x => x.Id == entityId, ct);
```

---

## 10. Business Rules

Panel chỉ khi có id chọn. Wave/Putaway/XD attach = evidence ops (ảnh lệch/kẹt) — không bắt buộc trước complete.

---

## 11. Exceptions

Giống P43 + `OPS_EXPORT_TYPE_INVALID` cho type mới.

---

## 12. Observability

KPI attachments by entityType (LOT/EXCEPTION/LPN/…).

---

## 13. Test Plan

Bind LOT/EXCEPTION/LPN/WAVE 201 · missing 404.  
Export INVENTORY_BALANCES xlsx.  
dbm 6 panels + inventory export.  
Regression verify P43.

---

## 14. DoD

- [ ] 6 entityTypes attach PASS  
- [ ] 8 ops export types PASS  
- [ ] ❌ owner=P44 = **0 còn thiếu**  
- [ ] verify_p44 + dbm + plan row ✅  

---

## 15. OOS

P45 items.

---

## 16. Downstream

P45 dùng Files API + mobile upload path.

---

## 17. Rollback

Revert allowlist/FE P44; giữ P43.

---

## 18. Bảo trì

Thêm type = handler + panel + export case.

---

## 19. Critique → 95%

| # | Rủi ro | Xử lý |
|---|---|---|
| 1 | Putaway proposal id UI | Panel trên row selected |
| 2 | Wave entity name | Khóa `WAVE` ↔ `PickingWaves.Id` |
| 3 | Inventory export perf | Cap 5000 + index sẵn |

**Maturity:** **95% Ready**

| FOUNDER | ☐ Proceed sau P43 · ☐ Hold |
|---|---|

---

## 20. EP

| EP | Goal |
|---|---|
| EP0 | Handlers 6 + allowlist |
| EP1 | FE panels 6 |
| EP2 | Ops export 8 + FE |
| EP3 | verify + dbm + docs |

---

## 21. Liên kết

- Upstream: [P43](file:///d:/1_Project/48_Nexustock/planning/phases/phase_43_ops_attachments_spreadsheet.md)  
- Downstream: [P45](file:///d:/1_Project/48_Nexustock/planning/phases/phase_45_line_import_rf_package_thumb.md)
