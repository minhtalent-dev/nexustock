# Thẩm định tiếp — Tính năng chưa phù hợp / không nên map 1:1 với 3 kho tham chiếu

**Ngày:** 2026-07-22  
**Loại:** Continuation audit (sau audit tổng [`AUDIT_REFERENCE_WAREHOUSE_PROJECTS.md`](AUDIT_REFERENCE_WAREHOUSE_PROJECTS.md) và sau **Phase 34 IQC** ✅)  
**Đối tượng:** Nexustock vs `1_GCM_Part` · `2_GCM_Shipping` · `warehouse-main`  
**Câu hỏi FOUNDER:**

> Còn tính năng nào **chưa phù hợp** với 3 dự án kho dùng làm tham chiếu để tạo nên Nexustock không?

**Verdict ngắn:**  
Nexustock **vẫn PHÙ HỢP** làm product thay thế lớp WMS cốt lõi.  
Phần “chưa phù hợp” chủ yếu là: (A) flow **Sharp/GCM nhà máy** không nên clone; (B) vài pattern **warehouse-main** khác mô hình multi-tenant WMS; (C) vài module Nexustock **vượt tham chiếu** — đúng product, không phải lệch sứ mệnh.

---

## 1. Bối cảnh cập nhật (sau P34)

| Hạng mục | Trạng thái |
|---|---|
| Roadmap code | Phase **01–35** ✅ (Module DoD) |
| Gap IQC UX / Gate (audit lần 1) | **Đã đóng** bằng P34 (`QcGate`, `/admin/qc` filter/history, optional `/mobile/qc`) |
| Nav Ops lens | **Đã đóng** P35 |
| Nghiệm thu chức năng | [`ACCEPTANCE_FUNCTION_PARITY_REFERENCE_WAREHOUSES.md`](ACCEPTANCE_FUNCTION_PARITY_REFERENCE_WAREHOUSES.md) |
| **`rf` gap còn thiếu** | [`GAP_FUNCTIONS_REFERENCE_TO_NEXUSTOCK.md`](GAP_FUNCTIONS_REFERENCE_TO_NEXUSTOCK.md) — form/model inventory |
| Evidence | [`evidence/phase_34_dbm/walkthrough.md`](evidence/phase_34_dbm/walkthrough.md) |
| Điểm Part sau P34 (ước lượng) | **8.5 → ~9.0** ở trục QC/IQC parity vận hành |

Audit này **không lặp** ma trận phủ tổng; tập trung **misfit / không map / dư-thiếu có chủ đích**.

---

## 2. Phân loại “chưa phù hợp”

| Mã | Ý nghĩa | Hành động khuyến nghị |
|---|---|---|
| **M1** | Có ở tham chiếu · **không** nên port vào Nexustock product generic | Giữ out-of-scope / Rules site-specific |
| **M2** | Có ở Nexustock · **không** có / không khớp mô hình 3 tham chiếu | Giữ — đây là giá trị hiện đại hóa |
| **M3** | Có ở tham chiếu · Nexustock **thiếu / khác hình** · có thể cần nếu cutover site | Wave riêng / cấu hình / adapter |
| **M4** | Khác stack/UX · **không** là thiếu nghiệp vụ | Training + UAT, không viết lại WinForms |

---

## 3. M1 — Flow tham chiếu **không phù hợp** để nhét vào Nexustock (cấm clone)

### 3.1 Từ GCM Part (`1_GCM_Part` · ~56 form nghiệp vụ)

| Form / năng lực GCM | Vì sao không phù hợp product | Nexustock thay thế? |
|---|---|---|
| `frm126` / `frm151` **VMI Accept** | VMI = hợp đồng nhà cung cấp tại kho; gắn Sharp site | ❌ Không port. Mở rộng Inbound/Rules khi FOUNDER chốt site |
| `frm138*` **Invoice Divide** | Chia hóa đơn nội bộ nhà máy | ❌ Out of product core |
| `frm128` **CAP Organize** | Quy trình CAP Sharp | ❌ |
| `frm127` **Part Formation FPC** | Quy trình FPC đặc thù | ❌ |
| `frm197` **Enter CTL_CD** / Ford-style code | Mã điều khiển OEM | ❌ Master attribute / custom field sau |
| `frm03e` (Shipping) **Wafer Lot Separation** | Semiconductor wafer | ❌ Domain khác Parts generic |
| `frm103` **Resin Reprint** | Tem resin đặc thù | ◐ Label template config — không form riêng |
| `frm112` **Part Set** / `frm124` Wait Set | Set thiết bị nhà máy | ◐ LPN/kit wave riêng nếu cần |
| `frm107a` **Output Kowake** (xuất nhỏ) | Nghiệp vụ kowake shopfloor | ◐ Outbound partial pick đã có; UI “kowake” không clone |
| Handy **BT-1500 COM/CSV desktop** | Thick-client + DLL máy | ◐ Mobile RF + Local Agent — **M4** |
| Excel report / form report IQC cứng | Báo cáo gắn DB SQL Server | ◐ Observability + export API — không clone Excel macro |

**Kết luận M1-Part:** Clone các form trên vào Nexustock sẽ **làm lệch** product generic và tăng nợ bảo trì. Đúng chiến lược: **1 Nexustock + cấu hình site**, không 2 product Part/Shipping.

### 3.2 Từ GCM Shipping (`2_GCM_Shipping`)

| Form / năng lực | Vì sao không phù hợp clone | Thay thế Nexustock |
|---|---|---|
| `frm106` **Export Approval** (form FPRD) | Workflow phê duyệt xuất khẩu nhà máy | RBAC + exception + (tuỳ) approval wave — **không** form WinForms |
| `frm108` **Destination Registration** cứng | Master đích ship gắn EXE | MasterData partners/locations |
| `frm110` **Invoice Input** shipping | Hóa đơn vận chuyển nội bộ | ERP integration / outbound docs — không form GCM |
| Non-admin **auto-update EXE** | Mô hình desktop | Docker / web deploy — **M4** |
| Keyence handy **CSV file drop** | Protocol thiết bị cũ | Mobile scan + Agent — adapter sau nếu bắt buộc |

### 3.3 Từ warehouse-main (Laravel)

| Năng lực | Vì sao không map 1:1 | Ghi chú |
|---|---|---|
| **Filament** resource UX | Stack khác; không phải thiếu WMS | Next.js admin đã đủ CRUD/ops |
| **Team + TeamWarehouseAssignment** | Model team Filament SaaS-ish | Nexustock: Tenant + Role/Permission — đủ; team matrix = optional |
| **WorkflowApproval** generic entity | Pattern Laravel approval | Nexustock: permission gates + Exceptions; multi-step approval **chưa** là SoT |
| **Post** / CMS-like model | Không thuộc WMS kho | Không port |
| **Product images gallery** / brand-heavy retail | Góc thương mại B2C-ish | MasterData đủ field; gallery = nice-to-have |
| **Inventory sync manager** Filament page | Đồng bộ kho kiểu app retail | Nexustock: ERP contract + webhook — khác shape |

---

## 4. M2 — Module Nexustock **vượt / khác** 3 tham chiếu (vẫn phù hợp sứ mệnh)

Các tính năng này **không có** (hoặc rất mỏng) ở 3 tham chiếu — **không** coi là “lệch tham chiếu”; đây là lý do Nexustock tồn tại.

| Module Nexustock | GCM Part | GCM Shipping | warehouse-main | Đánh giá fit |
|---|:---:|:---:|:---:|---|
| Rules engine | ❌ | ❌ | ❌ | ✅ Fit product |
| Putaway slotting | ❌ | ❌ | ❌ | ✅ |
| Allocation / reservation | ❌ | ◐ FIFO form | ❌ | ✅ |
| Wave + Put-Wall | ❌ | ❌ | ❌ | ✅ |
| Cross-docking | ❌ | ❌ | ❌ | ✅ |
| Material genealogy cascade | ❌ | ❌ | ❌ | ✅ |
| Labor tracking KPI | ❌ | ❌ | ❌ | ✅ |
| Task interleaving | ❌ | ❌ | ❌ | ✅ |
| Local Agent (scale/print WSS) | ◐ printer DLL | ◐ | ❌ | ✅ hiện đại hóa |
| Webhook Outbox/DLQ | ❌ | ❌ | ❌ | ✅ |
| Feature flags / Observability | ❌ | ❌ | ◐ | ✅ |
| Readiness / Cutover freeze | ❌ | ❌ | ❌ | ✅ |
| i18n VI/EN product-wide | ◐ en/ja/zh EXE | ◐ | ◐ | ✅ (ja/zh packs = M3) |
| QcGate + IQC UX map (P34) | ◐ forms | N/A | ❌ | ✅ đã căn Part |

**Không khuyến nghị** gỡ các module trên chỉ vì tham chiếu không có.

---

## 5. M3 — Còn thiếu / khác hình — **có thể** cần khi cutover site (không chặn product DoD)

Ưu tiên theo rủi ro thay GCM thực tế:

| # | Gap | Nguồn tham chiếu | Mức | Gợi ý phase / hướng |
|---|---|---|---|---|
| 1 | **Adapter Handy Keyence** (CSV/COM parity) | Part + Shipping | Trung bình | Wave thiết bị — chỉ khi sàn bắt buộc máy cũ |
| 2 | **Export approval** multi-step | Shipping `frm106` | Trung bình | Approval workflow mỏng trên Outbound (không clone form) |
| 3 | **VMI / invoice divide** | Part | Trung–cao *nếu* site Sharp | Site module / Rules — **không** vào core generic |
| 4 | **Kowake / inner-only output** UX | Part `frm107a/b` | Thấp–TB | Cấu hình outbound pick mode |
| 5 | **Lot valid extend / discard / rework** form parity | Part `frm115–119`, `frm196` | Thấp | Một phần qua Inventory adjust + RMA + QC; UI wizard tùy site |
| 6 | **Team–warehouse** assignment | warehouse-main | Thấp | Optional Identity extension |
| 7 | **WorkflowApproval** đa cấp | warehouse-main | Thấp | Chỉ khi compliance bắt buộc |
| 8 | Locale **ja / zh** packs | GCM | Thấp | Mở catalog i18n (nền P31–33 sẵn) |
| 9 | **Brand / Category tree / Product images** | warehouse-main | Thấp | MasterData enrich |
| 10 | Báo cáo Excel nhà máy 1:1 | Part/Shipping | Trung bình | Reporting wave + template mapping |

**Sau P34:** Gap IQC queue/history/gate **không còn** trong danh sách chặn cutover Parts QC.

---

## 6. M4 — Khác hình thái vận hành (phù hợp chiến lược, cần change management)

| Chủ đề | Tham chiếu | Nexustock | Kết luận |
|---|---|---|---|
| Client | WinForms EXE | Web Admin + Mobile web | **Phù hợp** hiện đại hóa; cần training |
| DB | SQL Server trực tiếp form | PostgreSQL + API | **Phù hợp**; cấm ETL ngầm vào form |
| Dual app Part vs Shipping | 2 EXE | **1 product** + warehouse/role/FF | **Phù hợp** — đã khóa từ audit 1 + P34 |
| In tem / cân | DLL máy | Local Agent | **Phù hợp** |
| Đa kho / tenant | Thường 1 nhà máy | Multi-tenant | **Phù hợp** product; GCM không phải SoT |

---

## 7. Ma trận “có nên làm tiếp không?” (quyết định nhanh)

| Ý tưởng | Fit với 3 tham chiếu? | Làm trong core Nexustock? |
|---|---|---|
| Clone VMI/CAP/wafer/Ford forms | Có ở GCM · **không** fit product | **Không** |
| Clone BT-1500 desktop | Có ở GCM · lệch kiến trúc | **Không** — chỉ adapter |
| Thêm Wave/Labor/TI | Không có ở tham chiếu · fit WMS hiện đại | **Đã có — giữ** |
| Export approval nhẹ | Shipping cần · fit một phần | **Có thể** wave nhỏ |
| ja/zh | GCM có · fit i18n | **Có thể** khi thị trường cần |
| Tách 2 product Part/Shipping | GCM có 2 EXE · **phá** chiến lược Nexustock | **Không** |
| IQC Gate + UX | Part cần · đã làm P34 | **Xong** |

---

## 8. Điểm số cập nhật (sau P34 + misfit review)

| Trục | Audit lần 1 | Lần này | Ghi chú |
|---|:---:|:---:|---|
| Phủ Part (core + IQC) | 8.5 | **9.0** | P34 đóng gate/UX IQC |
| Phủ Shipping | 8.5 | **8.5** | Approval/invoice vẫn M3 |
| Phủ warehouse-main | 9.5 | **9.5** | Không đổi |
| Tránh clone Sharp-only (độ “đúng product”) | — | **9.5** | M1 rõ |
| **Tổng phù hợp tham chiếu → product** | **9.0** | **9.2 / 10** | Misfit đã phân loại; không blocker |

---

## 9. Kết luận cho FOUNDER

1. **Không có tính năng core Nexustock nào “sai hướng”** so với sứ mệnh hợp nhất 3 tham chiếu.  
2. **Có nhiều tính năng GCM** (VMI, CAP, wafer, invoice divide, COM handy…) **chưa phù hợp** để đưa vào core — giữ **M1**.  
3. **Có gap M3** nếu cutover site Sharp/Shipping cứng — xử lý bằng wave cấu hình/adapter, **không** bằng clone WinForms.  
4. Module “vượt tham chiếu” (Wave, Labor, TI, Readiness…) thuộc **M2** — đúng đòn bẩy, không phải lệch.  
5. Sau P34, trục IQC/QC **đã căn** Part đủ để migration enablement; residual là UAT lot-seed + training.

### Khuyến nghị ưu tiên (nếu tiếp tục)

| Ưu tiên | Việc | Khi nào |
|---|---|---|
| P0 ops | Go-live pack P30 (ngoài code) | Trước production |
| P1 optional | Export approval mỏng (Shipping) | Khi site Shipping cutover |
| P2 optional | Handy adapter Keyence | Khi máy cũ bắt buộc |
| P3 site | VMI/CAP chỉ khi contract Sharp | Không vào core |
| P4 | ja/zh catalogs | Khi có yêu cầu locale |

---

## 10. Nguồn & liên kết

| Tài liệu | Path |
|---|---|
| Audit tổng (lần 1) | [`AUDIT_REFERENCE_WAREHOUSE_PROJECTS.md`](AUDIT_REFERENCE_WAREHOUSE_PROJECTS.md) |
| Phase 34 IQC | [`phases/phase_34_iqc_ux_map_gcm.md`](phases/phase_34_iqc_ux_map_gcm.md) |
| UX map IQC | [`IQC_UX_MAP_GCM_PART.md`](IQC_UX_MAP_GCM_PART.md) |
| Master plan | [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) |
| GCM Part forms | `D:\1_Project\2_GCM\1_GCM_Part\frm*.vb` |
| GCM Shipping forms | `D:\1_Project\2_GCM\2_GCM_Shipping\GCM_PART\frm*.vb` |
| warehouse-main models | `D:\1_Project\warehouse-main\warehouse-main\src\app\Models\` |

---

**Chữ ký thẩm định:** JARVIS · 2026-07-22 · **APPROVED — misfit đã phân loại; product vẫn phù hợp tham chiếu (9.2/10)**
