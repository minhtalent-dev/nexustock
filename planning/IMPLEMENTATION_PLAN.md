# KẾ HOẠCH TỔNG THỂ TRIỂN KHAI DỰ ÁN NEXUSTOCK

Dự án **Nexustock** là giải pháp quản lý - vận hành kho thế hệ mới, thay thế hệ thống desktop cũ bằng nền tảng Web SPA hiện đại, PostgreSQL độc lập và roadmap triển khai theo chuẩn WMS production.

Roadmap dùng mô hình **4 stage / 30 phase nhỏ**. Mỗi phase là một deliverable độc lập, có đủ setup, database, backend/API, frontend/RF/mobile, execution flow, validation, exception, observability, test, acceptance, maintenance, extension và rollback.

---

## Definition of done cho mỗi phase

Một phase chỉ được xem là hoàn tất khi:

* Có database contract hoặc xác nhận rõ không cần database.
* Có API contract hoặc xác nhận rõ chỉ là tài liệu/cấu hình.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end không mơ hồ.
* Có validation rule, exception mapping và observability.
* Có test plan gồm unit, integration, E2E/negative/regression nếu phù hợp.
* Có acceptance criteria đo được.
* Có maintenance notes, extension points và rollback notes.
* Không còn placeholder generic.

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
    title Nexustock - 30 phase production roadmap
    dateFormat  YYYY-MM-DD
    section MVP vận hành chắc
    Phase 01: Project foundation :active, p01, 2026-07-01, 3d
    Phase 02: Master data foundation : p02, after p01, 4d
    Phase 03: User, RBAC & audit foundation : p03, after p02, 4d
    Phase 04: Inbound receiving : p04, after p03, 5d
    Phase 05: QC hold/release : p05, after p04, 4d
    Phase 06: Inventory by location & movement : p06, after p05, 5d
    Phase 07: Outbound picking & packing basic : p07, after p06, 6d
    Phase 08: Cycle count & stock adjustment : p08, after p07, 5d
    Phase 09: RF/mobile core scan : p09, after p08, 6d
    Phase 10: Exception framework MVP : p10, after p09, 4d
    section Advanced WMS
    Phase 11: Rule engine foundation : p11, after p10, 5d
    Phase 12: Putaway slotting : p12, after p11, 5d
    Phase 13: Allocation & reservation : p13, after p12, 5d
    Phase 14: Replenishment : p14, after p13, 4d
    Phase 15: LPN pallet management : p15, after p14, 5d
    Phase 16: Serial tracking : p16, after p15, 4d
    Phase 17: RMA return flow : p17, after p16, 5d
    Phase 18: Wave picking : p18, after p17, 5d
    Phase 19: Material genealogy : p19, after p18, 5d
    section Enterprise integration
    Phase 20: Local Agent foundation : p20, after p19, 4d
    Phase 21: Scale integration : p21, after p20, 4d
    Phase 22: Label printing : p22, after p21, 4d
    Phase 23: ERP/WMS legacy contract : p23, after p22, 6d
    Phase 24: Webhook & integration reliability : p24, after p23, 5d
    Phase 25: Operational observability : p25, after p24, 5d
    Phase 26: Production deployment : p26, after p25, 5d
    section Optimization & automation
    Phase 27: Cross-docking : p27, after p26, 5d
    Phase 28: Labor tracking : p28, after p27, 4d
    Phase 29: Task interleaving : p29, after p28, 5d
    Phase 30: Hardening & production acceptance : p30, after p29, 7d
```

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
| 30 | [Hardening & production acceptance](file:///d:/1_Project/48_Nexustock/planning/phases/phase_30_hardening_production_acceptance.md) | Kiểm thử tổng thể, hardening, UAT, cutover và rollback rehearsal trước go-live. |
---

## Tài liệu phase cũ

Các phase cũ đã được chuyển vào thư mục tạm:

[planning/phases/temp](file:///d:/1_Project/48_Nexustock/planning/phases/temp)


