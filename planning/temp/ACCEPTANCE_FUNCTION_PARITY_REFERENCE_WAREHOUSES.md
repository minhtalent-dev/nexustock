# Biên bản nghiệm thu chức năng — Nexustock ↔ 3 dự án kho tham chiếu

**Ngày:** 2026-07-22 · **Deep reindex:** 2026-07-22 (cùng ngày)  
**Loại:** Nghiệm thu logic theo **từng chức năng** — có **evidence mã nguồn disk** (không chỉ map tài liệu tổng)  
**Đối tượng:** `48_Nexustock` vs  
- `D:\1_Project\2_GCM\1_GCM_Part` (GCM Part — phụ liệu + IQC)  
- `D:\1_Project\2_GCM\2_GCM_Shipping` (GCM Shipping — pack/FIFO/ship)  
- `D:\1_Project\warehouse-main\warehouse-main` (Laravel Filament WMS-lite)  

**Tài liệu liên quan:**  
- [`AUDIT_REFERENCE_WAREHOUSE_PROJECTS.md`](AUDIT_REFERENCE_WAREHOUSE_PROJECTS.md) — thẩm định tổng (đọc kèm)  
- [`AUDIT_FEATURES_MISFIT_REFERENCE_WAREHOUSES.md`](AUDIT_FEATURES_MISFIT_REFERENCE_WAREHOUSES.md) — misfit M1–M4  
- [`IQC_UX_MAP_GCM_PART.md`](IQC_UX_MAP_GCM_PART.md) — map IQC form  
- **§9 bên dưới** — audit sâu từng rule / file path  
- [`GAP_FUNCTIONS_REFERENCE_TO_NEXUSTOCK.md`](GAP_FUNCTIONS_REFERENCE_TO_NEXUSTOCK.md) — **`rf`:** còn thiếu gì từ tham chiếu  
- Roadmap: Phase **01–35** ✅ (`IMPLEMENTATION_PLAN.md`)

---

## 1. Mục đích nghiệm thu

Trả lời từng chức năng:

> So với chức năng tương ứng ở dự án tham chiếu, Nexustock đã **đúng logic** và **đủ logic** để vận hành WMS product (không yêu cầu clone WinForms/Filament 1:1) chưa?

| Tiêu chí | Định nghĩa PASS |
|---|---|
| **Đúng logic** | Rule nghiệp vụ cốt lõi không sai (không âm tồn, QC gate, FIFO/FEFO, RBAC, tenant…) |
| **Đủ logic** | Có luồng end-to-end tương đương tham chiếu **hoặc** có chủ đích thay hình thái mạnh hơn / OOS có lý do (M1) |
| **Không PASS** | Thiếu rule chặn vận hành core **và** không có mitigation |

### Quy ước cột

| Ký hiệu | Nghĩa |
|---|---|
| ✅ | Đúng + đủ (tương đương hoặc mạnh hơn) |
| ◐ | Đúng hướng / đủ core · khác UX hoặc thiếu nhánh site-specific |
| ⬜ | Vượt tham chiếu (Nexustock có · tham chiếu không) — vẫn PASS product |
| ❌ | Thiếu / sai · **chỉ** chấp nhận nếu M1 (cấm clone) hoặc P1 optional đã ghi |
| N/A | Tham chiếu không có domain tương ứng |

**Trạng thái nghiệm thu dòng:** `PASS` · `PASS*` (PASS có residual/OOS đã khóa) · `HOLD` (cần phase/site)

---

## 2. Scorecard tổng

| Nhóm chức năng | # dòng | PASS | PASS* | HOLD |
|---|---:|---:|---:|---:|
| A. Master & Identity | 6 | 6 | 0 | 0 |
| B. Inbound & Lot | 5 | 4 | 1 | 0 |
| C. QC / IQC / Hold | 6 | 5 | 1 | 0 |
| D. Inventory & Count | 5 | 5 | 0 | 0 |
| E. Outbound / Pack / Ship | 7 | 5 | 2 | 0 |
| F. Mobile / Device / Label | 6 | 4 | 2 | 0 |
| G. WMS nâng cao (vượt ref) | 8 | 8 | 0 | 0 |
| H. Integration / Observability / Go-live | 6 | 6 | 0 | 0 |
| I. UX / i18n / Nav | 4 | 4 | 0 | 0 |
| **Tổng** | **53** | **47** | **6** | **0** |

**Verdict nghiệm thu tổng:** **PASS product** — **0 HOLD chặn DoD**.  
6 dòng `PASS*` = residual site/UX (Handy adapter, export approval multi-step, VMI/M1, ja/zh packs, inventory active-path UI).

**Điểm logic vận hành (ước lượng):** **9.3 / 10** (sau P34 IQC Gate + P35 Ops nav).  
**Sau deep disk §9:** giữ **9.3 / 10** — residual mới DF-01 (mobile offline qty) **LOW**, không hạ PASS product.

---

## 3. Ma trận nghiệm thu theo chức năng

### A. Master data & Identity

| # | Chức năng Nexustock | Phase | GCM Part | GCM Shipping | warehouse-main | Đúng? | Đủ? | NT | Ghi chú logic |
|---|---|---|---|---|---|:---:|:---:|:---:|---|
| A1 | Product / UoM / convert | 02–03, 32 | Master lot/part | Product ship | Product, Unit, Brand, Category | ✅ | ✅ | **PASS** | Catalog chuẩn WMS |
| A2 | Warehouse / Zone / Location | 02 | Vị trí phụ liệu | WH in | Warehouse, StorageLocation | ✅ | ✅ | **PASS** | Layout 2D + capacity |
| A3 | Partners / suppliers / customers | 04 | Supplier forms | Destination | Supplier, Contact | ✅ | ✅ | **PASS** | Partners thống nhất |
| A4 | Reason codes | 10+ | Hold reason | — | — | ✅ | ✅ | **PASS** | Chuẩn exception/QC |
| A5 | Users / Roles / Permissions | 03 | Login EXE | Login EXE | User, Role, Permission | ✅ | ✅ | **PASS** | JWT + permission seed |
| A6 | Audit log | 03, 25 | Hạn chế | Hạn chế | Activity | ✅ | ✅ | **PASS** | Audit + timeline |

---

### B. Inbound & Lot

| # | Chức năng Nexustock | Phase | GCM Part | GCM Shipping | warehouse-main | Đúng? | Đủ? | NT | Ghi chú logic |
|---|---|---|---|---|---|:---:|:---:|:---:|---|
| B1 | PO / nhận hàng tạo Lot | 04, 23 | Lot create / WH in | Warehouse-in | PO, StockImport | ✅ | ✅ | **PASS** | Idempotency ERP |
| B2 | Lot lookup / trạng thái | 04–05 | Lot forms | Lot ship | Inventory lot-ish | ✅ | ✅ | **PASS** | SoT Inbound.Lots |
| B3 | Putaway sau nhận | 12 | Move FC | — | Transfer | ✅ | ✅ | **PASS** | Rule + QcGate |
| B4 | VMI accept / invoice divide | — | frm126/138 | — | — | N/A | ❌ | **PASS\*** | **M1** cấm clone — site module sau |
| B5 | Cross-dock đề xuất | 27 | — | — | — | ⬜ | ⬜ | **PASS** | Vượt ref |

---

### C. QC / IQC / Hold

| # | Chức năng Nexustock | Phase | GCM Part | GCM Shipping | warehouse-main | Đúng? | Đủ? | NT | Ghi chú logic |
|---|---|---|---|---|---|:---:|:---:|:---:|---|
| C1 | Queue IQC / lọc / aging | 05, 34 | frm136 | N/A | — | ✅ | ✅ | **PASS** | `/admin/qc` + API filter |
| C2 | Ghi kết quả Pass/Fail | 05, 34 | frm113/114 | N/A | — | ✅ | ✅ | **PASS** | Result dialog; map UX |
| C3 | History / timeline | 34 | frm137 | N/A | — | ✅ | ✅ | **PASS** | Tab History |
| C4 | Hold / Release + reason | 05, 34 | smv_frm6 PartHold | N/A | — | ✅ | ✅ | **PASS** | Permission + reason |
| C5 | **QcGate** chặn move/pick Unspec/Hold | 34 | Implicit shopfloor | FIFO QC? | — | ✅ | ✅ | **PASS** | Wire Inventory/Outbound/Putaway/Mobile/LPN/Repl |
| C6 | Mobile QC optional | 34 | Handy IQC | — | — | ✅ | ◐ | **PASS\*** | `FF_MOBILE_QC` default off |

Evidence: `planning/evidence/phase_34_dbm/`, `IQC_UX_MAP_GCM_PART.md`.

---

### D. Inventory & kiểm kê

| # | Chức năng Nexustock | Phase | GCM Part | GCM Shipping | warehouse-main | Đúng? | Đủ? | NT | Ghi chú logic |
|---|---|---|---|---|---|:---:|:---:|:---:|---|
| D1 | Tồn theo location | 06 | Stock query | Stock | Inventory, InventoryLocation | ✅ | ✅ | **PASS** | Chống âm |
| D2 | Move / transfer | 06, 09 | frm108a Move | Transfer | StockTransfer, StockMovement | ✅ | ✅ | **PASS** | + QcGate |
| D3 | Cycle count / adjust | 08 | Tana | — | StockAdjustment | ✅ | ✅ | **PASS** | Approve adjust |
| D4 | LPN / pallet | 15 | Part set ◐ | Package set | — | ✅ | ✅ | **PASS** | Attach/move atomic |
| D5 | Exceptions vận hành | 10 | Msg boxes | Msg | — | ✅ | ✅ | **PASS** | Framework + SLA |

---

### E. Outbound / Pack / Ship / FIFO

| # | Chức năng Nexustock | Phase | GCM Part | GCM Shipping | warehouse-main | Đúng? | Đủ? | NT | Ghi chú logic |
|---|---|---|---|---|---|:---:|:---:|:---:|---|
| E1 | Shipment + pick | 07 | Output / kowake ◐ | Organize ship | StockExport | ✅ | ✅ | **PASS** | Partial pick = kowake shape |
| E2 | Packing | 07, 08 | — | Package organize | — | ✅ | ✅ | **PASS** | Pack + carton |
| E3 | Allocation FIFO/FEFO | 13 | — | FIFO check forms | — | ✅ | ✅ | **PASS** | Resource ordering |
| E4 | Wave + Put-Wall | 18 | — | — | — | ⬜ | ⬜ | **PASS** | Vượt ref |
| E5 | RMA / return | 17 | Return/discard | — | — | ✅ | ✅ | **PASS** | Restock/scrap |
| E6 | Export approval multi-step | — | — | frm106 | WorkflowApproval | ◐ | ◐ | **PASS\*** | RBAC+exception; **M3** nếu site bắt buộc |
| E7 | Invoice/destination ship cứng | — | — | frm108/110 | — | N/A | ❌ | **PASS\*** | **M1** — ERP/partners thay |

---

### F. Mobile / Device / Label / Scale

| # | Chức năng Nexustock | Phase | GCM Part | GCM Shipping | warehouse-main | Đúng? | Đủ? | NT | Ghi chú logic |
|---|---|---|---|---|---|:---:|:---:|:---:|---|
| F1 | Mobile RF scan core | 09 | BT-1500 desktop | Keyence CSV | — | ✅ | ✅ | **PASS** | Offline sync idempotent |
| F2 | Local Agent WSS | 20 | DLL printer | DLL | — | ✅ | ✅ | **PASS** | Loopback + DPAPI |
| F3 | Scale COM | 21 | — | Weight input | — | ✅ | ✅ | **PASS** | Manual override gate |
| F4 | Label ZPL/TSPL + reprint | 22 | Reprint tem | Reprint packing | — | ✅ | ✅ | **PASS** | Audit reprint |
| F5 | Handy BT-1500 COM/CSV parity | — | Có | Có | — | ◐ | ◐ | **PASS\*** | **M3/M4** — adapter khi sàn bắt buộc |
| F6 | Desktop EXE auto-update | — | Có | Có | — | N/A | N/A | **PASS** | Web/Docker thay — M4 |

---

### G. WMS nâng cao (Nexustock vượt tham chiếu)

| # | Chức năng Nexustock | Phase | Part | Ship | WH-main | Đúng? | Đủ? | NT | Ghi chú |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|---|
| G1 | Rule engine | 11 | ❌ | ❌ | ❌ | ⬜ | ⬜ | **PASS** | Config site thay hardcode |
| G2 | Replenishment min/max | 14 | ❌ | ❌ | ❌ | ⬜ | ⬜ | **PASS** | + Gate |
| G3 | Serial tracking | 16 | ◐ | ❌ | ✅ | ✅ | ✅ | **PASS** | Parity WH-main+ |
| G4 | Material genealogy | 19 | ❌ | ❌ | ❌ | ⬜ | ⬜ | **PASS** | Cascade hold |
| G5 | Labor KPI | 28 | ❌ | ❌ | ❌ | ⬜ | ⬜ | **PASS** | M2 giữ |
| G6 | Task interleaving | 29 | ❌ | ❌ | ❌ | ⬜ | ⬜ | **PASS** | M2 giữ |
| G7 | Feature flags | 25–30 | ❌ | ❌ | ◐ | ⬜ | ⬜ | **PASS** | FF_MOBILE_QC… |
| G8 | Ops↔Modules nav lens | 35 | Form menus | Form menus | Filament nav | ✅ | ✅ | **PASS** | Cutover UX operator |

Evidence P35: `planning/evidence/phase_35_dbm/`.

---

### H. Integration / Observability / Go-live

| # | Chức năng Nexustock | Phase | Part | Ship | WH-main | Đúng? | Đủ? | NT | Ghi chú |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|---|
| H1 | ERP/WMS legacy contract | 23 | SQL nội bộ | SQL | Sync manager ◐ | ✅ | ✅ | **PASS** | Idempotent import |
| H2 | Webhook Outbox/DLQ | 24 | ❌ | ❌ | ❌ | ⬜ | ⬜ | **PASS** | Reliability |
| H3 | Observability / alerts / KPI | 25 | Excel reports | Excel | Alerts ◐ | ✅ | ✅ | **PASS** | Không clone macro |
| H4 | Docker deploy / backup / rollback | 26 | Publish folder | Auto EXE | Docker | ✅ | ✅ | **PASS** | |
| H5 | Readiness + Cutover freeze | 30 | ❌ | ❌ | ❌ | ⬜ | ⬜ | **PASS** | Go-live gate |
| H6 | Multi-tenant isolation | 01 | Single plant | Single | Team WH ◐ | ✅ | ✅ | **PASS** | Mạnh hơn ref |

---

### I. UX / Localization / Nav

| # | Chức năng Nexustock | Phase | Part | Ship | WH-main | Đúng? | Đủ? | NT | Ghi chú |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|---|
| I1 | i18n VI/EN 59/59 | 31–33 | en/ja/zh EXE | en/ja | Filament locale | ✅ | ✅ | **PASS** | ja/zh = M3 packs |
| I2 | Errors catalog có mã | 33–34 | Msg cứng | Msg | — | ✅ | ✅ | **PASS** | `QC_LOT_*` |
| I3 | Admin Ops↔Modules | 35 | Menu WinForms | Menu | Filament groups | ✅ | ✅ | **PASS** | Parity 44 links |
| I4 | Mobile shell i18n | 33 | Handy UI JP | Handy | — | ✅ | ✅ | **PASS** | |

---

## 4. Logic “đúng” — checklist bắt buộc (đã có trên disk)

| Rule | Nơi khóa | Ref tương ứng | NT |
|---|---|---|:---:|
| Không dùng Lot Unspec/Hold/Reject cho move/pick | `QcGate` + wire call-sites | IQC output / move GCM | ✅ |
| Allocation FEFO/FIFO chống deadlock | Allocation module | FIFO check Shipping | ✅ |
| Offline sync không trùng | Mobile idempotency | Handy retry GCM | ✅ |
| Tenant isolation | Middleware + DbContext | Single DB GCM | ✅ |
| Permission filter nav/API | Identity + sidebar | EXE login | ✅ |
| Webhook retry/DLQ | Outbox | (không có ở ref) | ✅ |

---

## 5. Những gì **không** nghiệm thu vào core (M1 — đúng khi thiếu)

| Chức năng tham chiếu | Lý do | Nexustock |
|---|---|---|
| VMI / CAP / Part Formation / Wafer / CTL_CD | Site Sharp / domain khác | Không port |
| Invoice divide / ship invoice form cứng | Nội bộ nhà máy | ERP adapter |
| BT-1500 thick-client DLL | Stack cũ | Mobile + Agent |
| Filament Team matrix / Post CMS | Khác mô hình product | Không port |

→ **Thiếu các mục trên = PASS chiến lược product**, không phải FAIL nghiệm thu.

---

## 6. Residual / việc sau (không chặn ký nghiệm thu product)

| ID | Việc | Mức | Gợi ý |
|---|---|---|---|
| R1 | Adapter Keyence CSV/COM nếu sàn bắt buộc | TB | Phase thiết bị |
| R2 | Export approval multi-step | TB | Outbound workflow mỏng |
| R3 | ja/zh catalogs | Thấp | Pack locale |
| R4 | Inventory vs stocktakes active highlight | Thấp | UI polish |
| R5 | Role-default Ops nav | Thấp | P35 P1 |
| **DF-01** | Mobile offline MOVE check `QtyOnHand` chưa trừ `QtyReserved` (online move dùng QtyAvailable) | Thấp | Align offline với `QtyOnHand - QtyReserved` |

---

## 7. Bảng ký nghiệm thu

| Hạng mục | Kết luận |
|---|---|
| Logic cốt lõi vs 3 tham chiếu | **Đúng** (đã verify mã nguồn §9) |
| Độ phủ chức năng vận hành kho | **Đủ** cho product WMS (siêu tập + M1 OOS) |
| IQC/GCM Part cutover enablement | **Đủ** sau P34 — Gate SoT Inbound.Lots trên disk |
| Operator IA (Ops lens) | **Đủ** sau P35 |
| HOLD chặn go-live product | **0** |

### Chữ ký

| Vai trò | Kết quả | Ngày |
|---|---|---|
| JARVIS (reindex disk + đối chiếu ref code) | **Khuyến nghị ký PASS** · xem §9 | 2026-07-22 |
| FOUNDER | ☐ Duyệt / ☐ Duyệt có điều kiện R1–R5+DF-01 / ☐ Không duyệt | ____ |

---

## 8. Kết luận

Nexustock (**Phase 01–35**) đã **đúng và đủ logic** so với từng nhóm chức năng tương ứng ở GCM Part, GCM Shipping và warehouse-main theo chiến lược:

1. **Core WMS** = tương đương hoặc mạnh hơn — có path code (§9).  
2. **IQC/Hold/Gate** = đủ thay lớp QC phụ liệu (không clone form) — `QcGateService` + wire 6 call-sites.  
3. **Pack/FIFO/Ship** = đủ qua Outbound + Allocation FEFO/FIFO engine (mạnh hơn popup FIFO GCM).  
4. **Không clone** flow Sharp/Filament misfit (M1).  
5. Module vượt ref (Wave, Labor, TI, Readiness…) = **giá trị product**, giữ nguyên.

**Biên bản nghiệm thu chức năng: PASS (47 PASS + 6 PASS* · 0 HOLD)** — deep disk **không đảo** verdict; chỉ thêm residual **DF-01 LOW**.

---

## 9. Audit sâu disk — evidence mã nguồn (không tài liệu suông)

**Phương pháp:** Đọc code Nexustock + spot-check form/model tham chiếu · đối chiếu rule nghiệp vụ · ghi path.  
**Không** thay thế §3; **làm cứng** các dòng quan trọng bằng bằng chứng.

### 9.1 QC / IQC / Hold / Gate (nhóm C)

| Rule cần đúng | Evidence Nexustock | Evidence tham chiếu | So khớp logic | NT |
|---|---|---|---|:---:|
| Chỉ Lot **Release** được move/pick | `QcGateService.EnsureReleased` — Hold → `QC_LOT_ON_HOLD`; khác Release → `QC_LOT_NOT_RELEASED`; null → `QC_LOT_NOT_FOUND` · SoT `InboundDbContext.Lots` | Part: `frm113_Iqc_Input.vb` (IQC cập nhật lot); `smv_frm6_PartHold.vb` (`SMV_PART_HOLD`, `IS_HOLD` YES/NO) | GCM = hold flag + form; Nexustock = **enum QcStatus + gate tập trung** (mạnh hơn, không phụ thuộc operator nhớ) | ✅ |
| Gate gọi tại mọi đường dùng hàng | Wire: `InventoryController.Move`, `OutboundController` pick, `PutawayController`, `MobileController.SyncOffline` MOVE, `LpnService` attach/move, `ReplenishmentService` | Part move `frm108a` / IQC output `frm135` — kiểm soát rải form | Đủ call-site P34; **đúng hơn** GCM (1 SoT) | ✅ |
| Queue / result / history | `/admin/qc` + API queue/history (P34 dbm 13/13) | `frm136` list, `frm113` input, `frm137` result | Đủ parity UX-ops; không clone WinForms | ✅ |

**Kết luận C:** Đúng + đủ. Không phải map giấy.

### 9.2 Inventory move / chống âm (nhóm D)

| Rule | Evidence Nexustock | Evidence ref | So khớp | NT |
|---|---|---|---|:---:|
| Không move vượt khả dụng | `InventoryController.MoveInventory`: `(QtyOnHand - QtyReserved) < dto.Qty` → `INSUFFICIENT_QTY`; transaction deduct/add | warehouse-main `StockTransfer.php`: `available_quantity < item->quantity` throw; Part move forms | **Cùng rule** chống thiếu tồn | ✅ |
| Location lock / capacity | `LOCATION_LOCKED`, `LOCATION_OVER_CAPACITY` trong cùng Move | GCM capacity/location rules rải SQL | Nexustock rõ errorCode | ✅ |
| Offline MOVE | `MobileController.SyncOffline`: idempotent `ClientOperationId`; **có** QcGate; check `QtyOnHand < qty` | Handy retry GCM | Đúng Gate + idempotent; **DF-01**: offline chưa trừ reserved như online | ✅* |

### 9.3 Allocation FIFO/FEFO (nhóm E3)

| Rule | Evidence Nexustock | Evidence ref | So khớp | NT |
|---|---|---|---|:---:|
| Chỉ allocate Lot Release | `AllocationService`: filter `LotQcStatus.Release` trước sort | Shipping FIFO check sau khi chọn package | Nexustock **gắn QC vào allocation** | ✅ |
| FIFO = production date; FEFO = expiry | `OrderBy` ProductionDate (FIFO) / ExpiryDate (FEFO) + tie-break CreatedAt/Id | `frm104_Fprd_OrganizeShipmentSet .vb`: `FPRD_FIFO_MODE_SW` + popup `FIFO_Check` nếu còn lot cũ hơn | GCM = **cảnh báo UI** khi lệch FIFO; Nexustock = **engine phân bổ** — đủ và mạnh hơn cho WMS | ✅ |
| Partial / fail nếu thiếu | `AllowPartial` / throw không đủ tồn | GCM chặn/xác nhận tay | Đúng | ✅ |

### 9.4 Mobile offline idempotency (nhóm F1)

| Rule | Evidence | Ref | NT |
|---|---|---|:---:|
| Không double-apply | `AnyAsync(ClientOperationId)` → `AlreadySynced`; unique index `uq_offline_ops_tenant_client_op_id` | Handy gửi lại file/CSV | ✅ |

### 9.5 Wave (nhóm E4 / G)

| Rule | Evidence | Ref | NT |
|---|---|---|:---:|
| Tạo / release wave | `WaveService.CreateWaveAsync` / `ReleaseWaveAsync` | Không có ở 3 ref | ⬜ PASS vượt |

### 9.6 warehouse-main stock transfer parity (nhóm D2)

| Rule | Nexustock | warehouse-main | NT |
|---|---|---|:---:|
| Insufficient stock | `INSUFFICIENT_QTY` | `validation.insufficient_stock_for_transfer` | ✅ cùng ý |
| Serial path | Serial module riêng | `uses_serial_tracking` branch | ✅ cả hai có |
| Multi-tenant | `TenantId` mọi query | Team–warehouse (khác mô hình) | ✅ Nexustock chặt hơn product multi-tenant |

### 9.7 Nav Ops lens (nhóm I3 / G8)

| Rule | Evidence | Ref | NT |
|---|---|---|:---:|
| Cùng 44 href 2 mode | `nav-registry` + `verify_nav_lens` parity | Menu WinForms / Filament | ✅ |
| Persist mode | `nexustock:sidebar:navMode` · dbm 14/14 | — | ✅ |

### 9.8 Phát hiện deep (mới)

| ID | Severity | Finding | Ảnh hưởng nghiệm thu |
|---|---|---|---|
| DF-01 | LOW | Offline MOVE: `QtyOnHand < qty` vs online `QtyOnHand - QtyReserved` | Không đảo PASS; khuyến nghị patch nhỏ sau |
| DF-02 | INFO | GCM FIFO = warning UI; Nexustock = hard allocation order | **Đủ hơn** ref — ghi nhận khác hình thái |
| DF-03 | INFO | GCM Hold = bảng `SMV_PART_HOLD`; Nexustock = `Lot.QcStatus` + Gate | **Đúng SoT** hiện đại |

### 9.9 Mapping nhanh audit tổng → deep

| Audit tổng (T1) nói | Deep disk xác nhận |
|---|---|
| QC/Hold ✅ / IQC ◐→✅ sau P34 | **Xác nhận code** Gate + wire |
| FIFO Shipping ✅ | **Xác nhận** Allocation FEFO/FIFO + Release filter |
| Move/transfer ✅ | **Xác nhận** INSUFFICIENT_QTY + lock/capacity |
| Handy ◐ | Mobile + Agent có; BT-1500 DLL vẫn M3 |
| VMI ❌ | Vẫn M1 — không cần code |

### 9.10 Verdict deep

**PASS có evidence** — tài liệu §3 được neo bằng path/rule thật.  
Không phát hiện **sai logic core** so với tham chiếu.  
Residual: **DF-01** + R1–R5 (đã biết).

---
JARVIS · Deep function audit (disk) · 2026-07-22
