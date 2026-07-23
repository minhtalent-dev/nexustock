# KẾ HOẠCH TỔNG THỂ TRIỂN KHAI DỰ ÁN NEXUSTOCK

Dự án **Nexustock** là giải pháp quản lý - vận hành kho thế hệ mới, thay thế hệ thống desktop cũ bằng nền tảng Web SPA Next.js hiện đại (kết hợp Tailwind CSS, Shadcn UI), PostgreSQL độc lập, hỗ trợ Redis Cache (optional, recommended) cho backend và roadmap triển khai theo chuẩn WMS production.

Roadmap dùng mô hình **4 stage / 38 phase nhỏ (+ Phase 31a catalog modules)**. Mỗi phase là một deliverable độc lập, có đủ setup, database, backend/API, frontend/RF/mobile, execution flow, validation, exception, observability, test, acceptance, maintenance, extension và rollback.

**Post-35 (generic go-live):** P36 L2-P0 → P37 L3 Pilot → P38 UI Design System (Option B; không block P36).

---

## Tiêu chuẩn Sẵn sàng & Hoàn thành (DoR & DoD)

Mọi phase triển khai bắt buộc phải vượt qua các cổng kiểm soát được định nghĩa chi tiết tại [delivery_governance.md](file:///d:/1_Project/48_Nexustock/planning/enterprise/delivery_governance.md):

- **Definition of Ready (DoR):** Chỉ bắt đầu phát triển khi nghiệp vụ rõ ràng, API/DB contract đã khóa và upstream dependencies đã hoàn tất nghiệm thu.
- **Definition of Done (DoD):** Một phase chỉ được xem là hoàn thành khi:
  - Có database & API contract chuẩn hóa, không còn placeholder.
  - Test suite tự động (Unit + Integration) pass 100%.
  - Luồng nghiệp vụ chính chạy được E2E trên UI/RF.
  - Không vi phạm nguyên tắc bảo mật, không âm tồn kho.
  - Có Trace ID và log nghiệp vụ đầy đủ.
  - Cung cấp đầy đủ **Bằng chứng kiểm thử (Evidence)** cho Product Owner (FOUNDER) ký duyệt.

---

## Nguyên tắc maintenance roadmap

* [planning/phases](file:///d:/1_Project/48_Nexustock/planning/phases) chỉ chứa phase mới nhất.
* [planning/phases/temp](file:///d:/1_Project/48_Nexustock/planning/phases/temp) chỉ dùng tham chiếu lịch sử, không chỉnh nếu không có lý do.
* Khi đổi contract ở phase trước, phải cập nhật phase phụ thuộc downstream.
* Khi thêm permission, status, bảng hoặc API mới, phải cập nhật test và observability tương ứng.
* Không giới hạn số dòng phase; ưu tiên chi tiết đủ để executor triển khai an toàn.

---

## Lộ trình triển khai

```mermaid
gantt
    title Nexustock - 4 Workstreams Parallel Roadmap
    dateFormat  YYYY-MM-DD
    axisFormat  %m-%d
    
    section Core WMS Workstream
    Phase 02: Master data foundation     :crit, p02, 2026-07-04, 4d
    Phase 04: Inbound receiving          :crit, p04, after p03, 5d
    Phase 05: QC hold/release            :crit, p05, after p04, 4d
    Phase 06: Inventory & movement       :crit, p06, after p05, 5d
    Phase 07: Outbound picking/packing   :crit, p07, after p06, 6d
    Phase 08: Cycle count                :crit, p08, after p07, 5d
    Phase 12: Putaway slotting           :crit, p12, after p11, 5d
    Phase 13: Allocation & reservation   :crit, p13, after p12, 5d
    Phase 14: Replenishment              :p14, after p13, 4d
    Phase 15: LPN pallet management      :p15, after p14, 5d
    Phase 16: Serial tracking            :p16, after p15, 4d
    Phase 17: RMA return flow            :p17, after p16, 5d
    Phase 18: Wave picking               :crit, p18, after p17, 5d
    Phase 19: Material genealogy         :crit, p19, after p18, 5d
    Phase 27: Cross-docking              :p27, after p26, 5d
    
    section Platform & Logic Workstream
    Phase 01: Project foundation         :crit, active, p01, 2026-07-01, 3d
    Phase 03: User, RBAC & audit         :crit, p03, after p02, 4d
    Phase 10: Exception framework MVP    :p10, after p09, 4d
    Phase 11: Rule engine foundation     :crit, p11, after p10, 5d
    Phase 25: Operational observability  :p25, after p24, 5d
    Phase 26: Production deployment       :p26, after p25, 5d
    
    section Integration & Devices
    Phase 09: RF/mobile core scan        :p09, after p08, 6d
    Phase 20: Local Agent foundation     :p20, after p07, 4d
    Phase 21: Scale integration          :p21, after p20, 4d
    Phase 22: Label printing             :p22, after p21, 4d
    Phase 23: ERP/WMS legacy contract    :p23, after p22, 6d
    Phase 24: Webhook & integration reliability  :p24, after p23, 5d
    Phase 30: Readiness Gate             :p30, after p29, 7d
    Phase 31: i18n Foundation+Admin      :p31, after p30, 5d
    Phase 31a: i18n Catalog Modules      :p31a, after p31, 2d
    Phase 32: i18n Master-data           :p32, after p31a, 4d
    Phase 33: i18n Mobile+Errors+Close   :p33, after p32, 4d
    Phase 34: IQC UX Map GCM Part        :p34, after p33, 4d
    Phase 35: Admin Nav Ops↔Modules Lens :p35, after p34, 3d
    Phase 36: Inventory Integrity L2-P0  :p36, after p35, 5d
    Phase 37: Go-Live L3 Customer Pilot  :p37, after p36, 7d
    Phase 38: UI Design System Pass      :p38, after p35, 12d
```
### Critical Path

> **Ghi chú:** Tag `crit` trong Gantt hiển thị màu đỏ trong VS Code Mermaid Preview / Mermaid Live Editor. Trên GitHub/Gitea, các task dưới đây cần được ưu tiên theo dõi thủ công.

Critical path là chuỗi phase dài nhất quyết định ngày go-live sớm nhất. Mọi trễ trên chuỗi này lan trực tiếp sang Phase 30.

**Chuỗi Critical Path:**
`P01 → P02 → P03 → P04 → P05 → P06 → P07 → P08 → P11 → P12 → P13 → P18 → P19 → P30`

| Phase | Tên | Dev-days (mid) | Impact nếu trễ 1 tuần |
|---|---|:---:|---|
| 01 | Project foundation | 3 | Toàn bộ 30 phase bị delay |
| 02 | Master data | 4 | FK, validation mọi module bị block |
| 03 | RBAC & audit | 4 | Security gate, menu rule bị block |
| 04 | Inbound receiving | 5 | Lot source of truth, P23/P27 bị delay |
| 05 | QC hold/release | 3.5 | Stock promise sai, P06 bị block |
| 06 | Inventory & movement | 5 | Ledger integrity, P07-P19 bị block |
| 07 | Outbound picking | 6 | Outbound flow + P20/P23 bị delay |
| 08 | Cycle count | 4.5 | P09 bị block |
| **11** | **Rule engine** | **5** | **P12, P13, P18 toàn bộ bị block** |
| **12** | **Putaway slotting** | **5** | **P13 bị block** |
| **13** | **Allocation & reservation** | **6** | **P18, P27, P29 — tổng 3+ phase trễ** |
| **18** | **Wave picking** | **5** | **P19, P29 bị delay** |
| 19 | Material genealogy | 5 | P30 UAT bị thiếu recall feature |
| 30 | Readiness Gate | 7 | Go-live bị trễ tương đương |

> **⚠️ Phase 13 (Allocation) là nút thắt cao nhất:** Trễ 1 tuần tại P13 gây trễ tối thiểu 3 tuần cho P18 → P19 → P30. Deep spec Allocation phải hoàn tất TRƯỚC KHI bắt đầu code P13.

### Các mốc Milestone chính

- **Milestone 1: Core WMS MVP (Sau Phase 10)** - Kho vận hành cơ bản bằng tay kết hợp quét mã RF đã sẵn sàng.
- **Milestone 2: Advanced Rules (Sau Phase 19)** - Putaway, allocation, replenishment, và wave picking tự động hóa hoạt động ổn định.
- **Milestone 3: Local Integration (Sau Phase 24)** - Hoàn tất tích hợp phần cứng bàn cân, máy in tem nhãn và đồng bộ hóa đơn hàng với SAP.
- **Milestone 4: Production Ready (Sau Phase 30)** - Vượt qua hardening, UAT, diễn tập rollback, ký biên bản bàn giao go-live.
- **Milestone 5: Product Localization (Sau Phase 33)** - ✅ **59/59** pages Web VI/EN, **0 backlog**, switcher locale (sidebar + mobile shell), catalogs parity (12 modules PascalCase + merge + semantic keys), errorCodeLabel + message localized. (P31 → P31a → P32 → **P33 ✅** · `rp4`+`rp5` 2026-07-22)

### Chính sách Buffer rủi ro

Dựa trên cấu hình team **1 Developer chính**, áp dụng chính sách buffer bắt buộc sau:
- **Core WMS:** 10% buffer dự phòng lỗi logic nghiệp vụ.
- **Local Agent & Hardware (COM/ZPL):** 25% buffer dự phòng lỗi driver, kết nối phần cứng và mixed-content HTTPS/WSS.
- **ERP Integration (SAP):** 35% buffer dự phòng do phụ thuộc vào tiến độ, môi trường sandbox và tài liệu của bên thứ ba (SAP team).
- **UAT & Hardening:** 40% buffer cho các tình huống diễn tập sập mạng, sập DB và fix bug phát hiện cuối kỳ.

---

## 6 khối năng lực bắt buộc xuyên suốt

| Khối | Phase chính | Mục tiêu |
|---|---|---|
| RF/mobile operation design | Phase 09 | Mọi thao tác kho quan trọng có flow scan handheld/mobile |
| Exception framework | Phase 10 | Chuẩn hóa sai mã, sai Lot, sai vị trí, thiếu/dư hàng, mất mạng, cân lỗi, in lỗi |
| Rule engine | Phase 11-14 | Putaway, allocation, picking, replenishment, FEFO/FIFO, zone constraint |
| Integration layer | Phase 20-24 | Local Agent, ERP/WMS legacy, API contract, webhook, import/export |
| Operational observability | Phase 25 | Audit log, activity timeline, dashboard KPI, alert, trace ID |
| Master data governance | Phase 02-03 | Item, UOM, package, location, zone, partner, reason code, permission catalog |

---

## Danh mục phase mới nhất

| Phase | Tài liệu | Deliverable |
|---|---|---|
| 01 | [Project foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_01_project_foundation.md) | Thiết lập nền tảng dự án để đội phát triển có thể chạy, build và mở rộng Nexustock nhất quán. |
| 02 | [Master data foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_02_master_data_foundation.md) | Chuẩn hóa dữ liệu nền WMS để mọi nghiệp vụ sau dùng chung một catalog nhất quán. |
| 03 | [User, RBAC & audit foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_03_user_rbac_audit_foundation.md) | Thiết lập bảo mật nền: user, role, permission, JWT/session và audit log cho mọi thay đổi dữ liệu. |
| 04 | [Inbound receiving](file:///d:/1_Project/48_Nexustock/planning/phases/phase_04_inbound_receiving.md) | Nhận hàng từ PO/Invoice, tạo Lot và ghi transaction nhập kho. |
| 05 | [QC hold/release](file:///d:/1_Project/48_Nexustock/planning/phases/phase_05_qc_hold_release.md) | Kiểm soát chất lượng Lot sau nhận: hold, release, reject, quarantine. |
| 06 | [Inventory by location & movement](file:///d:/1_Project/48_Nexustock/planning/phases/phase_06_inventory_location_movement.md) | Quản lý tồn kho theo vị trí và chuyển vị trí an toàn, chống âm kho. |
| 07 | [Outbound picking & packing basic](file:///d:/1_Project/48_Nexustock/planning/phases/phase_07_outbound_picking_packing_basic.md) | Xuất kho cơ bản từ shipment đến picking, packing và trừ tồn. |
| 08 | [Cycle count & stock adjustment](file:///d:/1_Project/48_Nexustock/planning/phases/phase_08_cycle_count_stock_adjustment.md) | Kiểm kê chu kỳ, khóa vị trí, ghi nhận chênh lệch và phê duyệt điều chỉnh tồn. |
| 09 | [RF/mobile core scan](file:///d:/1_Project/48_Nexustock/planning/phases/phase_09_rf_mobile_core_scan.md) | Chuẩn hóa thao tác handheld/mobile cho inbound, movement, picking, stocktake và packing. |
| 10 | [Exception framework MVP](file:///d:/1_Project/48_Nexustock/planning/phases/phase_10_exception_framework_mvp.md) | Chuẩn hóa xử lý ngoại lệ vận hành cho sai mã, sai Lot, sai vị trí, thiếu/dư hàng, lỗi thiết bị. |
| 11 | [Rule engine foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_11_rule_engine_foundation.md) | Tạo nền rule engine dạng bảng, có priority, condition, action và execution log. |
| 12 | [Putaway slotting](file:///d:/1_Project/48_Nexustock/planning/phases/phase_12_putaway_slotting.md) | Đề xuất vị trí cất hàng theo rule, capacity, zone và đặc tính vật tư. |
| 13 | [Allocation & reservation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_13_allocation_reservation.md) | Giữ hàng theo đơn xuất, ưu tiên, khách hàng, Lot, hạn dùng và trạng thái QC. |
| 14 | [Replenishment](file:///d:/1_Project/48_Nexustock/planning/phases/phase_14_replenishment.md) | Tự tạo nhiệm vụ bổ sung pick face từ reserve location theo min/max. |
| 15 | [LPN pallet management](file:///d:/1_Project/48_Nexustock/planning/phases/phase_15_lpn_pallet_management.md) | Quản lý Pallet/LPN để gom Lot và di chuyển hàng loạt bằng một mã. |
| 16 | [Serial tracking](file:///d:/1_Project/48_Nexustock/planning/phases/phase_16_serial_tracking.md) | Truy vết từng đơn vị sản phẩm bằng Serial Number. |
| 17 | [RMA return flow](file:///d:/1_Project/48_Nexustock/planning/phases/phase_17_rma_return_flow.md) | Xử lý hàng trả về, QC phân loại, tái nhập/cách ly/scrap. |
| 18 | [Wave picking](file:///d:/1_Project/48_Nexustock/planning/phases/phase_18_wave_picking.md) | Gom nhiều đơn xuất thành wave để tối ưu lấy hàng. |
| 19 | [Material genealogy](file:///d:/1_Project/48_Nexustock/planning/phases/phase_19_material_genealogy.md) | Truy vết cây Lot cha/con và khoanh vùng lỗi chất lượng. |
| 20 | [Local Agent foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_20_local_agent_foundation.md) | Tạo Windows Local Agent kết nối Web UI với thiết bị cục bộ qua localhost WebSocket. |
| 21 | [Scale integration](file:///d:/1_Project/48_Nexustock/planning/phases/phase_21_scale_integration.md) | Tích hợp cân điện tử qua COM và fallback cân tay có kiểm soát. |
| 22 | [Label printing](file:///d:/1_Project/48_Nexustock/planning/phases/phase_22_label_printing.md) | In tem mã vạch qua ZPL/TSPL, print job và reprint audit. |
| 23 | [ERP/WMS legacy contract](file:///d:/1_Project/48_Nexustock/planning/phases/phase_23_erp_wms_legacy_contract.md) | Chuẩn hóa API contract, import/export và mapping với ERP/WMS cũ. |
| 24 | [Webhook & integration reliability](file:///d:/1_Project/48_Nexustock/planning/phases/phase_24_webhook_integration_reliability.md) | Webhook tin cậy với retry, backoff, dead-letter và replay. |
| 25 | [Operational observability](file:///d:/1_Project/48_Nexustock/planning/phases/phase_25_operational_observability.md) | Thiết lập audit, activity timeline, KPI, alert và trace ID xuyên hệ thống. |
| 26 | [Production deployment](file:///d:/1_Project/48_Nexustock/planning/phases/phase_26_production_deployment.md) | Đóng gói production bằng Docker, health check, backup/restore và rollback. |
| 27 | [Cross-docking](file:///d:/1_Project/48_Nexustock/planning/phases/phase_27_cross_docking.md) | Đề xuất chuyển tiếp trực tiếp hàng vừa nhận sang đơn xuất phù hợp. |
| 28 | [Labor tracking](file:///d:/1_Project/48_Nexustock/planning/phases/phase_28_labor_tracking.md) | Đo năng suất theo task, user, ca, zone và loại thao tác. |
| 29 | [Task interleaving](file:///d:/1_Project/48_Nexustock/planning/phases/phase_29_task_interleaving.md) | Gợi ý task kế tiếp để giảm di chuyển rỗng nhưng không phá rule vận hành. |
| 30 | [Readiness Gate](file:///d:/1_Project/48_Nexustock/planning/phases/phase_30_hardening_production_acceptance.md) | Kiểm thử tổng thể, hardening, UAT, cutover và rollback rehearsal trước go-live. |
| 31 | [Localization Foundation + Shell/Admin](file:///d:/1_Project/48_Nexustock/planning/phases/phase_31_localization_vi_en.md) | next-intl, switcher, sidebar, **44** pages (admin+shell); Errors skeleton. |
| 31a | [i18n Catalog Modules](file:///d:/1_Project/48_Nexustock/planning/phases/phase_31a_i18n_catalog_modules.md) | Tách `messages/{vi\|en}/{Namespace}.json` (PascalCase 1:1) + merge; key mới = semantic sections. |
| 32 | [Localization Master-data](file:///d:/1_Project/48_Nexustock/planning/phases/phase_32_localization_master_data.md) | **8/8** MD; chỉ `MasterData.json` + semantic keys. |
| 33 | [Localization Mobile + Errors + Close](file:///d:/1_Project/48_Nexustock/planning/phases/phase_33_localization_mobile_errors.md) | **7/7** mobile + `Mobile.json`/`Errors.json`; khóa **59/59 + 0 backlog**. |
| 34 | [IQC UX Map GCM Part → Nexustock](file:///d:/1_Project/48_Nexustock/planning/phases/phase_34_iqc_ux_map_gcm.md) | Map form IQC GCM; `QcGate`; queue/history UX; optional mobile QC; UAT/training. |
| 35 | [Admin Nav Ops ↔ Modules Lens](file:///d:/1_Project/48_Nexustock/planning/phases/phase_35_admin_nav_ops_modules_lens.md) | Toggle Modules/Ops; polish Labor+RMA+Utilities; i18n Sidebar; parity href. **✅ ĐÓNG** (`rp4`+`rp5` 2026-07-22). |
| 36 | [Inventory Integrity L2-P0](file:///d:/1_Project/48_Nexustock/planning/phases/phase_36_inventory_integrity_l2_p0.md) | Hợp nhất allocation · invariant tồn · DF-01. **✅ ĐÓNG** (`rp4`+`rp5` Module DoD 100% · dbm · verify 14/0). |
| 37 | [Go-Live L3 Customer Pilot](file:///d:/1_Project/48_Nexustock/planning/phases/phase_37_golive_l3_customer_pilot.md) | UAT · cutover · hypercare. **✅ Module DoD 100%** · `PILOT_READY_CONDITIONAL` (`rp4`+`rp5`). |
| 38 | [UI Design System Pass](file:///d:/1_Project/48_Nexustock/planning/phases/phase_38_ui_design_system_pass.md) | Option B token + PageShell. **✅ ĐÓNG** (`rp4`+`rp5` · AUDIT ~8.2 · dbm 32/0). |

---

## Bảng theo dõi tiến độ triển khai

> **Hướng dẫn:** Khi hoàn thành một phase, cập nhật trạng thái thành `✅ Hoàn thành`, điền ngày hoàn thành và tóm tắt thông tin đã thực hiện vào cột tương ứng.

### Lộ trình 35 Phase triển khai chi tiết

| Phase | Phân hệ / Tính năng | Trạng thái | Nội dung chính và kết quả kiểm thử | Ngày hoàn thành | Ghi chú vận hành / Rollback plan |
|:---:|---|:---:|---|---|---|
| 01 | Multi-Tenant routing | ✅ Hoàn thành | Hoàn tất Tenant Middleware, Tenant DbContext filter, bảo mật chéo tenant, verify_tenant_isolation.ps1 pass 100%. | 2026-07-10 | Rollback: Tắt tenant middleware trong Program.cs. |
| 02 | Warehouse layout | ✅ Hoàn thành | Thiết lập cấu trúc Zone/Aisle/Bay/Level, DB migrations, seed data, quản lý dung tích sức chứa, UI 2D layout builder. | 2026-07-10 | Rollback: Dùng EF Core down-migration. |
| 03 | Product Catalog | ✅ Hoàn thành | Quản lý sản phẩm, đơn vị tính (UoM), quy đổi quy cách, kiểm thử tích hợp API, UI list/create/edit sản phẩm. | 2026-07-10 | — |
| 04 | Supplier / Partner | ✅ Hoàn thành | Đối tác cung cấp, khách hàng mua, phân quyền quản lý, API CRUD, UI Next.js quản trị danh mục đối tác. | 2026-07-11 | — |
| 05 | Inbound receiving basic | ✅ Hoàn thành | Tạo PO, nhận hàng thực tế, DTO API camelCase, DB migrations, UI tiếp nhận hàng và 3 script verify pass 100%. | 2026-07-11 | Rollback: Xóa migrations Inbound. |
| 06 | QC inspection basic | ✅ Hoàn thành | Hàng chờ QC, ghi kết quả, khóa lô hàng (Hold), giải phóng (Release), UI Next.js, verify_qc.ps1 pass 100%. | 2026-07-11 | — |
| 07 | Outbound basic picking | ✅ Hoàn thành | Tạo đơn xuất (Shipment), chỉ định picking location, in bảng kê, ghi nhận kết quả pick, UI Next.js, verify_outbound.ps1 pass 100%. | 2026-07-12 | — |
| 08 | Outbound packing | ✅ Hoàn thành | Đóng gói kiện hàng, chọn loại thùng, in phiếu đóng gói, cập nhật trạng thái đơn hàng, UI Next.js, verify_packing.ps1 pass 100%. | 2026-07-12 | — |
| 09 | RF/mobile core scan | ✅ Hoàn thành | Hoàn tất các bảng DB MobileDevice/ScanEvent/OfflineOperation/MobileTask, API kiểm tra mã quét, offline-sync (chống trùng), giao việc tối ưu khoảng cách di chuyển, UI handheld (bẫy online/offline, auto focus), integration test pass 100% | 2026-07-13 | Hồ chung (Pool model) tự động gán việc theo vị trí gần nhất; Test integration pass 100% |
| 10 | Exception framework MVP | ✅ Hoàn thành | Hoàn tất module backend Exceptions, DB migrations, seed permissions/reason codes, hosted SLA Job, middleware bẫy lỗi tự động, đồng bộ real-time số dư về Inventory, giao diện exceptions page dashboard Next.js và tích hợp sidebar menu, E2E integration test pass 100% | 2026-07-13 | E2E integration test tự động & Middleware auto-capture pass 100% |
| 11 | Rule engine foundation | ✅ Hoàn thành | Hoàn tất module backend Rules, DB migrations, seed permissions, engine RuleEvaluator (EQUALS/NOT_EQUALS/IN/NOT_IN/GREATER_THAN/LESS_THAN), giao diện rules page dashboard Next.js, verify_rules.ps1 integration test pass 100% | 2026-07-13 | Test integration pass 100% |
| 12 | Putaway slotting | ✅ Hoàn thành | Hoàn tất module backend Putaway, migrations PostgreSQL, seed permissions, API cất hàng (lọc luật Rule Engine, tính toán 2D Grid layout & khoảng cách Manhattan, cất hàng dùng TransactionScope, chống trùng lặp idempotency), UI Next.js 2D Grid map, kiểm thử tích hợp tự động pass 100%. | 2026-07-13 | — |
| 13 | Allocation & reservation | ✅ Hoàn thành | Hoàn tất module backend Allocation, migrations PostgreSQL, seed permissions, thuật toán phân bổ FEFO/FIFO chống deadlock (Resource Ordering), background worker dọn dẹp giữ hàng hết hạn theo lô, giao diện Next.js quản lý phân bổ và E2E test integration pass 100%. | 2026-07-13 | Đã chạy integration test verify_allocation.ps1 và kiểm thử UI thực tế qua browser subagent pass 100%. |
| 14 | Replenishment | ✅ Hoàn thành | Thiết lập quy trình bổ sung pick face tự động theo min/max: DDL bảng ReplenishmentRules/Tasks, API camelCase, thuật toán chọn nguồn tối ưu và tích hợp vào MobileTasks handheld | 2026-07-14 | Đã chạy tích hợp E2E verify_replenishment.ps1 và kiểm thử UI trực quan qua browser subagent pass 100% |
| 15 | LPN pallet management | ✅ Hoàn thành | Hoàn tất module backend Lpn, migrations PostgreSQL, seed permissions, thuật toán đóng hàng/rút hàng (split row xử lý QtyReserved) và dịch chuyển atomic nguyên khối pallet, UI Next.js quản lý và Mobile handheld quét dịch chuyển kệ, verify_lpn.ps1 integration test pass 100% | 2026-07-15 | Đã chạy tích hợp verify_lpn.ps1 và kiểm thử UI trực quan qua browser subagent pass 100%. |
| 16 | Serial tracking | ✅ Hoàn thành | Hoàn tất module backend Serial, migrations PostgreSQL, seed permissions, API quét nhận/validate picking/import CSV, UI Next.js quản lý timeline và màn hình quét nhận di động, verify_serial.ps1 integration test pass 100% | 2026-07-15 | Đã kiểm thử UI qua browser subagent pass 100% |
| 17 | RMA return flow | ✅ Hoàn thành | Hoàn tất module backend RMA, migrations PostgreSQL, seed permissions, logic xử lý trả hàng (Nhận -> QC Restock/Scrap), UI Next.js quản lý và xử lý QC nhanh, verify_rma.ps1 integration test pass 100% | 2026-07-15 | Đã kiểm thử tích hợp E2E và UI dashboard pass 100%. Đã fix lỗi Over Capacity bằng logic ưu tiên kệ Staging. |
| 18 | Wave picking | ✅ Hoàn thành | Hoàn tất module backend Wave, migrations PostgreSQL, seed permissions, API gom đợt/release/sort/complete, giao diện Next.js Wave Builder/Detail/Put-Wall động (nhấp nháy & bíp), E2E integration test verify_wave_picking.ps1 pass 100% | 2026-07-15 | Đã chạy tích hợp verify_wave_picking.ps1 pass 100% và hoàn thành bàn Put-Wall động. |
| 19 | Material genealogy | ✅ Hoàn thành | Hoàn tất module backend MaterialGenealogy, migrations PostgreSQL, seed permissions, thuật toán DFS chặn chu kỳ, Cascade Hold, Next.js page hiển thị cây phả hệ, verify_genealogy.ps1 integration test pass 100% | 2026-07-15 | Đã kiểm thử tích hợp E2E pass 100%. |
| 20 | Local Agent foundation | ✅ Hoàn thành | Hoàn tất backend Local Agent module, 4 bảng quản lý trạm/thiết bị/ghép cặp/lịch sử kết nối, API pairing/confirm/heartbeat/revoke, Windows Local Agent loopback `127.0.0.1` port `9000-9005`, DPAPI bảo vệ token, WebSocket envelope + HMAC guard, UI quản trị trạm và sidebar menu | 2026-07-16 | Đã chạy `tests/verify_local_agent.ps1` và `tests/verify_agent_websocket.ps1` pass 100%. Browser không giữ `AgentToken` bản rõ; staging/prod giữ yêu cầu `wss://`. |
| 21 | Scale integration | ✅ Hoàn thành | Hoàn tất tích hợp cân điện tử qua Local Agent WebSocket loopback, parser/filter cân ổn định, mock scale mode, API ghi đè cân tay, UI fallback manual override và kiểm soát đóng gói theo nguồn cân | 2026-07-16 | Full strict gate pass: Local Agent build, 3 script verify, và frontend lint ĐẠT TUYỆT ĐỐI 0 warnings ở cấu hình nghiêm ngặt nhất chuẩn Production. |
| 22 | Label printing | ✅ Hoàn thành | Hoàn tất hệ thống in tem nhãn: quản lý mẫu in, tạo lệnh in, in lại có lý do, Local Agent gửi lệnh đến máy in qua WebSocket, giao diện in sau đóng gói và kiểm thử tự động bằng mock printer output | 2026-07-17 | Full gate pass: renderer/sanitizer, WebSocket E2E, reprint audit và frontend lint. Pilot máy in thật không bắt buộc cho DoD dev. |
| 23 | ERP/WMS legacy contract | ✅ Hoàn thành | Hoàn tất cấu trúc DB, API tiếp nhận PO từ SAP, Idempotency key matrix, contract version guard (v1.1/v1.0), mapping resolver, import preview/commit atomic, UI quản lý logs/mappings/import CSV và 3 script PowerShell verify pass 100% | 2026-07-18 | Đã chạy 3 script verify pass 100%, Frontend lint 0 warnings. |
| 24 | Webhook & integration reliability | ✅ Hoàn thành | Triển khai Outbox Pattern cho Webhook, cơ chế Retry/Backoff tự động, DLQ (Dead Letter Queue), Replay thủ công và ký bảo mật HMAC-SHA256 trên HTTP Header. Giao diện Admin quản lý Webhook Subscriptions & Deliveries (DLQ tab, Replay). | 2026-07-18 | Đã chạy 3 script verify pass 100%, Frontend lint 0 warnings. |
| 25 | Operational observability  | ✅ Hoàn thành | Hoàn tất thiết kế observability: theo dõi trace log và che giấu dữ liệu nhạy cảm (sensitive masking), lưu vết dòng thời gian nghiệp vụ (Activity Timeline), tính toán KPI định kỳ qua hosted jobs và cảnh báo vận hành tự động (Operational Alerts) cùng giao diện Admin tích hợp đầy đủ. | 2026-07-18 | Full gate pass: Đã chạy 3 script verify pass 100%, Frontend lint ĐẠT TUYỆT ĐỐI 0 warnings. |
| 26 | Production deployment | ✅ Hoàn thành | Đã thiết lập Multi-stage Dockerfile cho API backend và standalone Next.js frontend, cấu hình docker-compose.prod.yml. Thiết lập endpoints health check an toàn, scripts backup/restore/rollback tự động và 3 script kiểm thử tự động vượt qua 100% test gates. | 2026-07-18 | Rollback: Chạy scripts/deploy-rollback.sh và trỏ tag về version ổn định trước đó. |
| 27 | Cross-docking | ✅ Hoàn thành | Thiết kế và triển khai module `Nexustock.Modules.CrossDocking`. Tích hợp DbContext chéo (`InboundDbContext`, `WaveDbContext`). Tạo API đánh giá, chấp thuận, từ chối candidates. Seed permission và Feature Flag `FF_CROSS_DOCKING_ENABLED`. Giao diện candidates list, detail timeline và tích hợp sidebar nav hoàn thành, lint pass 100%. | 2026-07-18 | E2E pass 100%: 6/6 kịch bản kiểm thử tích hợp nghiêm ngặt pass, UI subagent thực hiện đánh giá và duyệt candidate thành công trên browser. |
| 28 | Labor tracking | ✅ Hoàn thành | Đã hoàn tất toàn bộ logic backend (LaborTrackingService, DbContext, Controller), timeout background worker, UI Next.js (Dashboard + Recharts, Sessions timer), và verify_labor_tracking.ps1 tích hợp qua 14/15 scenarios pass, 1/15 scenario mutation feature flag skip hợp lệ. | 2026-07-20 | Đã kiểm thử E2E và UI walkthrough với browser subagent có evidence video. |
| 29 | Task interleaving | ✅ Hoàn thành | Gap-fix rp5: unique Open index, scoring v1, structured logs, unit tests 19/19, Accept shared TX, admin UI states, mobile Next task; verify PASS 13 / SKIP 2 / FAIL 0. | 2026-07-21 | Rollback: tắt `FF_TASK_INTERLEAVING_ENABLED`; migration Down drop `uq_recommendations_tenant_user_open`. |
| 30 | Readiness Gate | ✅ Hoàn thành | `rp5` 2026-07-21: Module DoD **100%** sau đối chiếu code/UI/verify/evidence. Verify PASS 9/0/0. Freeze middleware + admin Readiness/Cutover + flags. Video walkthrough đã fix. AC-08 waived; AC-05/12 SKIP. Go-live AC pack (02/03/06/09–11/13/14) = evidence ký production (ngoài code). | 2026-07-21 | [phase_30 §19.18](file:///d:/1_Project/48_Nexustock/planning/phases/phase_30_hardening_production_acceptance.md) |
| 31 | Localization Foundation + Admin | ✅ Hoàn thành | `rp4`+`rp5` 2026-07-21: Module DoD **100%**. verify PASS; dbm 83/4/0; catalogs qua P31a. | 2026-07-21 | [phase_31 §33](file:///d:/1_Project/48_Nexustock/planning/phases/phase_31_localization_vi_en.md) |
| 31a | i18n Catalog Modules | ✅ Hoàn thành | `rp4`+`rp5` 2026-07-21: Module DoD **100%**. modules PascalCase + loadMessages; verify 31a PASS; dbm 0 pageerror. | 2026-07-21 | [phase_31a §27](file:///d:/1_Project/48_Nexustock/planning/phases/phase_31a_i18n_catalog_modules.md) |
| 32 | Localization Master-data | ✅ Hoàn thành | `rp4`+`rp5` 2026-07-21: Module DoD **100%**. verify 32 PASS; dbm 16/16; 8/8 MD + CRUD. | 2026-07-21 | [phase_32 §27](file:///d:/1_Project/48_Nexustock/planning/phases/phase_32_localization_master_data.md) |
| 33 | Localization Mobile + Errors + Close | ✅ Hoàn thành | `rp4`+`rp5` 2026-07-22: Module DoD **100%**. verify 33 PASS; DBM **14/14**; Milestone 5 **59/59**; disk FAIL_COUNT=0. | 2026-07-22 | [phase_33 §27](file:///d:/1_Project/48_Nexustock/planning/phases/phase_33_localization_mobile_errors.md) |
| 34 | IQC UX Map GCM Part → Nexustock | ✅ Hoàn thành | **`rp4`+`rp5` Module DoD 100%** 2026-07-22. Disk FAIL=0; verify 16/16; dbm 13/13. | 2026-07-22 | [phase_34 §21–§22](file:///d:/1_Project/48_Nexustock/planning/phases/phase_34_iqc_ux_map_gcm.md) · [dbm](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_34_dbm/walkthrough.md) |
| 35 | Admin Nav Ops ↔ Modules Lens | ✅ Hoàn thành | **`rp4`+`rp5` Module DoD 100%** 2026-07-22. Disk FAIL=0; verify PASS; dbm 14/14 + video. | 2026-07-22 | [phase_35 §26–§27](file:///d:/1_Project/48_Nexustock/planning/phases/phase_35_admin_nav_ops_modules_lens.md) · [dbm](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_35_dbm/walkthrough.md) |
| 36 | Inventory Integrity L2-P0 | ✅ Hoàn thành | **`rp4`+`rp5` Module DoD 100%** 2026-07-22. Disk FAIL=0; verify 14/0; dbm 13/0 + video; L2-P0 CLOSED. | 2026-07-22 | [phase_36 §25–§26](file:///d:/1_Project/48_Nexustock/planning/phases/phase_36_inventory_integrity_l2_p0.md) · [dbm](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_36_dbm/walkthrough.md) |
| 37 | Go-Live L3 Customer Pilot | ✅ Hoàn thành | **Module DoD 100%** · `PILOT_READY_CONDITIONAL` (`rp4`+`rp5` 2026-07-22). verify_l3 **12/0**; dbm **21/0**; disk FAIL=0. Chờ FOUNDER ký. | 2026-07-22 | [phase_37 §25–§26](file:///d:/1_Project/48_Nexustock/planning/phases/phase_37_golive_l3_customer_pilot.md) · [rp45](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_37_rp45/validation_pass.md) |
| 38 | UI Design System Pass | ✅ Hoàn thành | **`rp4`+`rp5` Module DoD 100%** 2026-07-23. Disk FAIL=0; PageShell **56/57** (allowlist 1); verify_ui PASS; AUDIT ~**8.2**; **dbm 32/0**. | 2026-07-23 | [phase_38 §25–§26](file:///d:/1_Project/48_Nexustock/planning/phases/phase_38_ui_design_system_pass.md) · [rp45](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_38_rp45/validation_pass.md) · [dbm](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_38_dbm/walkthrough.md) |

### Quy ước trạng thái

| Ký hiệu | Ý nghĩa |
|:-------:|---------|
| ⬜ | Chưa bắt đầu |
| 🔄 | Đang thực hiện |
| ✅ | Hoàn thành |
| ⏸️ | Tạm dừng |
| ❌ | Hủy bỏ |

---

## Critical deep-spec backlog

Các deep spec dưới đây **không chặn Phase 01**. Đây là danh sách nâng cấp nhẹ để tránh overplanning: chỉ viết sâu ngay trước phase rủi ro cao tương ứng.

| Thời điểm bắt buộc | Deep spec cần viết | Mục tiêu nâng cấp | Lý do |
|---|---|---|---|
| Trước Phase 13 | Allocation algorithm spec | ✅ Đã hoàn thành (100% Ready) | Đã tích hợp cấu trúc DB, API DTOs, thuật toán chống Deadlock (Resource Ordering) và test cases vào tài liệu đặc tả Phase 13. |
| Trước Phase 20 | Local Agent threat model | Nâng Phase 20 từ 90% lên 95% execution-ready | Làm rõ attack vector, Origin allowlist, pairing token, WSS/certificate trust, code signing, spoofing test case. |
| Trước Phase 23 | SAP contract confirmation | ✅ Đã hoàn thành (100% Ready) | Làm rõ field mapping thật, error code matrix, idempotency, mapping resolver, preview/commit và 3 script verify pass 100%. |
| Trước Phase 26/30 | Migration rehearsal & incident playbook | ✅ Phase 26 95%; Phase 30 Module DoD ✅ (`rp4` 2026-07-21). AC-08 waived. | Go-live AC pack còn evidence trước ký production. |
| Trước Phase 36 | L2-P0 integrity deep spec | ✅ Phase 36 **Module DoD 100%** (`rp4`+`rp5` 2026-07-22) | Allocate SoT + CHECK on_hand + DF-01; verify 14/0; dbm 13/0; L2-P0 CLOSED. |
| Trước Phase 37 | L3 pilot UAT/cutover pack | ✅ Phase 37 **Module DoD 100%** (`rp4`+`rp5` 2026-07-22) · `PILOT_READY_CONDITIONAL` | disk FAIL=0; verify_l3 12/0; dbm 21/0; chờ FOUNDER ký. |
| Trước Phase 38 | UI Option B design system | ✅ Phase 38 **Module DoD 100%** (`rp4`+`rp5` 2026-07-23) | disk FAIL=0; AUDIT ~8.2; dbm 32/0; PageShell 56/57 + allowlist 1. |

### Nguyên tắc dùng backlog

- Không nâng toàn bộ 30 phase lên 95% từ đầu.
- Phase thường giữ mức 85-92% để bảo toàn tốc độ triển khai cho 1 Developer.
- Phase rủi ro cao chỉ được code sau khi deep spec tương ứng đạt 95%.
- Sau mỗi phase, cập nhật lại phần trăm execution spec bằng bài học thực tế.
## Tài liệu quản trị dự án

Để bảo đảm kiểm soát chất lượng bàn giao từ 1 Developer chính lên FOUNDER, toàn bộ quá trình thực thi phải tuân thủ nghiêm ngặt các tài liệu quản trị sau:

- **Quản trị phân phối:** [delivery_governance.md](file:///d:/1_Project/48_Nexustock/planning/enterprise/delivery_governance.md) (Quy định RACI, Tiêu chuẩn DoR/DoD và cổng kiểm soát phase).
- **Chiến lược kiểm thử:** [test_strategy.md](file:///d:/1_Project/48_Nexustock/planning/enterprise/test_strategy.md) (Quy định tháp test, test data mẫu, kịch bản tải và bảo mật).
- **Hướng dẫn phát hành & Cắt chuyển:** [release_runbook_governance.md](file:///d:/1_Project/48_Nexustock/planning/enterprise/release_runbook_governance.md) (Quy định quy trình go-live, backup, rollback và hypercare).
- **Nghiệm thu L2 (nền generic, trừ M1):** [ACCEPTANCE_L2_GENERIC_WMS_FOUNDATION.md](file:///d:/1_Project/48_Nexustock/planning/ACCEPTANCE_L2_GENERIC_WMS_FOUNDATION.md) — Simple **80.8** / Weighted **84.8** (2026-07-22). Không dùng `NEXUSTOCK_FUNCTION_ACCEPTANCE_REVIEW.md` (72.1 lẫn Sharp) làm SoT go-live.

## Tài liệu phase cũ

Các phase cũ đã được chuyển vào thư mục tạm:

[planning/phases/temp](file:///d:/1_Project/48_Nexustock/planning/phases/temp)

