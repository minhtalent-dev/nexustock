# KẾ HOẠCH TỔNG THỂ TRIỂN KHAI DỰ ÁN NEXUSTOCK

Dự án **Nexustock** là giải pháp quản lý - vận hành kho thế hệ mới, thay thế hệ thống desktop cũ bằng nền tảng Web SPA Next.js hiện đại (kết hợp Tailwind CSS, Shadcn UI), PostgreSQL độc lập, hỗ trợ Redis Cache (optional, recommended) cho backend và roadmap triển khai theo chuẩn WMS production.

Roadmap dùng mô hình **4 stage / 30 phase nhỏ**. Mỗi phase là một deliverable độc lập, có đủ setup, database, backend/API, frontend/RF/mobile, execution flow, validation, exception, observability, test, acceptance, maintenance, extension và rollback.

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
    Phase 24: Webhook reliability        :p24, after p23, 5d
    
    section Optimization & Release Gate
    Phase 28: Labor tracking             :p28, after p09, 4d
    Phase 29: Task interleaving          :p29, after p28, 5d
    Phase 30: Readiness Gate             :p30, after p29, 7d
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

---

## Bảng theo dõi tiến độ triển khai

> **Hướng dẫn:** Khi hoàn thành một phase, cập nhật trạng thái thành `✅ Hoàn thành`, điền ngày hoàn thành và tóm tắt thông tin đã thực hiện vào cột tương ứng.

| # | Phase | Trạng thái | Thông tin đã thực hiện | Ngày hoàn thành | Ghi chú |
|---|-------|:----------:|------------------------|:---------------:|---------|
| 01 | Project foundation | ✅ Hoàn thành | Thiết lập Monorepo (.NET API + 5 modules, Next.js frontend, Docker, env, README, health-ui, Swagger dev) | 2026-07-01 | — |
| 02 | Master data foundation | ✅ Hoàn thành | Hoàn tất cấu trúc bảng PostgreSQL cho Master Data, APIs CRUD danh mục, luồng Import CSV 2 bước (preview/commit) và frontend UI quản lý danh mục | 2026-07-02 | Có regression test cho import status flow và export filter |
| 03 | User, RBAC & audit foundation | ✅ Hoàn thành | Hoàn tất Identity module, JWT auth, refresh token rotation, user/role/permission API, audit log, tenant resolution, seed admin/permission catalog và integration test chính | 2026-07-03 | Build pass; Auth integration test pass |
| 04 | Inbound receiving | ✅ Hoàn thành | Hoàn tất thực thể backend Inbound, migrations PostgreSQL, seed permissions Inbound, giao diện Next.js (danh sách phiếu nhập, nhận hàng thực tế, tra cứu lô hàng), E2E test tích hợp và E2E UI Test trên browser pass 100% | 2026-07-10 | Kiểm soát dung sai (tolerance) nghiêm ngặt; E2E UI Test tự động qua Browser subagent pass 100%. |
| 05 | QC hold/release | ✅ Hoàn thành | Hoàn tất thực thể backend QC, database migrations, seed permissions, API kiểm định chất lượng & Hold/Release, giao diện Next.js, E2E test và E2E UI Test pass 100% | 2026-07-11 | Quyết định hold/release tức thì; E2E UI Test qua browser subagent pass 100%. |
| 06 | Inventory by location & movement | ✅ Hoàn thành | Hoàn tất module Inventory, migrations, seed permissions, API dịch chuyển kho, khóa/mở khóa vị trí, Capacity Guard, kiểm thử tích hợp tự động pass 100% | 2026-07-11 | Kiểm soát dung lượng vị trí (Capacity Guard) & Chốt chặn vị trí khóa; Integration Test pass 100% |
| 07 | Outbound picking & packing basic | ✅ Hoàn thành | Hoàn tất thực thể backend Outbound, migrations PostgreSQL, seed permissions, API sinh nhiệm vụ pick (FIFO/QC release/lock check), xác nhận pick trừ tồn kho thực tế & ghi ledger PICK_OUT, giao diện Next.js, integration test pass 100% | 2026-07-11 | Kiểm soát chặt QC Gate khi phân bổ/pick thực tế; Kiểm thử tích hợp tự động (Integration Test) pass 100% |
| 08 | Cycle count & stock adjustment | ✅ Hoàn thành | Hoàn tất thực thể backend Stocktake/Adjustment, migrations PostgreSQL, seed permissions, API kiểm kê/khóa kệ/phê duyệt chênh lệch L1-L3, giao diện Next.js quản lý và thực hiện kiểm kê, integration test pass 100% | 2026-07-13 | Phong tỏa kệ (location locks) tự động & Phê duyệt chênh lệch phân cấp L1-L3; Integration Test pass 100% |
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
| 19 | Material genealogy | ⬜ Chưa bắt đầu | — | — | — |
| 20 | Local Agent foundation | ⬜ Chưa bắt đầu | — | — | — |
| 21 | Scale integration | ⬜ Chưa bắt đầu | — | — | — |
| 22 | Label printing | ⬜ Chưa bắt đầu | — | — | — |
| 23 | ERP/WMS legacy contract | ⬜ Chưa bắt đầu | — | — | — |
| 24 | Webhook & integration reliability | ⬜ Chưa bắt đầu | — | — | — |
| 25 | Operational observability | ⬜ Chưa bắt đầu | — | — | — |
| 26 | Production deployment | ⬜ Chưa bắt đầu | — | — | — |
| 27 | Cross-docking | ⬜ Chưa bắt đầu | — | — | — |
| 28 | Labor tracking | ⬜ Chưa bắt đầu | — | — | — |
| 29 | Task interleaving | ⬜ Chưa bắt đầu | — | — | — |
| 30 | Readiness Gate | ⬜ Chưa bắt đầu | — | — | — |

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
| Trước Phase 23 | SAP contract confirmation | Nâng Phase 23 từ 90% lên 95% execution-ready | Làm rõ field mapping thật, error code matrix, idempotency, retry/replay, SAP sandbox readiness và owner xác nhận. |
| Trước Phase 26/30 | Migration rehearsal & incident playbook | Nâng Phase 26/30 từ 90% lên 95% execution-ready | Làm rõ migration rehearsal checklist, RTO/RPO, backup restore proof, rollback timing và incident response theo lỗi lớn. |

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

## Tài liệu phase cũ

Các phase cũ đã được chuyển vào thư mục tạm:

[planning/phases/temp](file:///d:/1_Project/48_Nexustock/planning/phases/temp)

