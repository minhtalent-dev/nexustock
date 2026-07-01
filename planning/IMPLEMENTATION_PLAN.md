# KẾ HOẠCH TỔNG THỂ TRIỂN KHAI DỰ ÁN NEXUSTOCK

Dự án **Nexustock** là giải pháp quản lý - vận hành kho thế hệ mới, thay thế hệ thống desktop cũ bằng nền tảng Web SPA hiện đại, PostgreSQL độc lập và roadmap triển khai theo chuẩn WMS production.

Roadmap đã được tách lại thành **4 stage / 30 phase nhỏ**. Mỗi phase là một deliverable độc lập, có đủ setup, database, backend/API, frontend/RF/mobile, execution flow, validation, exception, observability, test và acceptance criteria.

---

## Nguyên tắc phase chuẩn

Mỗi phase mới trong [planning/phases](file:///d:/1_Project/48_Nexustock/planning/phases) dùng cùng cấu trúc:

1. Mục tiêu
2. Phạm vi
3. Điều kiện đầu vào
4. Setup
5. Database
6. Backend/API
7. Frontend/RF/mobile
8. Execution flow
9. Validation & business rules
10. Exception handling
11. Observability
12. Test plan
13. Acceptance criteria
14. Out of scope
15. Dependencies

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

## Stage 1: MVP vận hành chắc

| Phase | Tài liệu | Deliverable |
|---|---|---|
| 01 | [Project foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_01_project_foundation.md) | Monorepo, Docker local, API/UI shell |
| 02 | [Master data foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_02_master_data_foundation.md) | Item, UOM, Package, Warehouse, Zone, Location, Partner, Reason Code |
| 03 | [User, RBAC & audit foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_03_user_rbac_audit_foundation.md) | User, Role, Permission, JWT, audit log |
| 04 | [Inbound receiving](file:///d:/1_Project/48_Nexustock/planning/phases/phase_04_inbound_receiving.md) | Nhận hàng, tạo Lot, transaction nhập |
| 05 | [QC hold/release](file:///d:/1_Project/48_Nexustock/planning/phases/phase_05_qc_hold_release.md) | QC, hold, release, reject |
| 06 | [Inventory by location & movement](file:///d:/1_Project/48_Nexustock/planning/phases/phase_06_inventory_location_movement.md) | Tồn theo vị trí, movement, location lock |
| 07 | [Outbound picking & packing basic](file:///d:/1_Project/48_Nexustock/planning/phases/phase_07_outbound_picking_packing_basic.md) | Đơn xuất, picking FIFO/FEFO, packing |
| 08 | [Cycle count & stock adjustment](file:///d:/1_Project/48_Nexustock/planning/phases/phase_08_cycle_count_stock_adjustment.md) | Kiểm kê, khóa vị trí, điều chỉnh tồn |
| 09 | [RF/mobile core scan](file:///d:/1_Project/48_Nexustock/planning/phases/phase_09_rf_mobile_core_scan.md) | Scan handheld/mobile cho core flow |
| 10 | [Exception framework MVP](file:///d:/1_Project/48_Nexustock/planning/phases/phase_10_exception_framework_mvp.md) | Khung xử lý ngoại lệ vận hành |

---

## Stage 2: Advanced WMS

| Phase | Tài liệu | Deliverable |
|---|---|---|
| 11 | [Rule engine foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_11_rule_engine_foundation.md) | Rule set, condition, action, execution log |
| 12 | [Putaway slotting](file:///d:/1_Project/48_Nexustock/planning/phases/phase_12_putaway_slotting.md) | Đề xuất vị trí cất hàng |
| 13 | [Allocation & reservation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_13_allocation_reservation.md) | Giữ hàng theo đơn xuất |
| 14 | [Replenishment](file:///d:/1_Project/48_Nexustock/planning/phases/phase_14_replenishment.md) | Bổ sung pick face |
| 15 | [LPN pallet management](file:///d:/1_Project/48_Nexustock/planning/phases/phase_15_lpn_pallet_management.md) | Pallet/LPN, di chuyển hàng loạt |
| 16 | [Serial tracking](file:///d:/1_Project/48_Nexustock/planning/phases/phase_16_serial_tracking.md) | Truy vết serial |
| 17 | [RMA return flow](file:///d:/1_Project/48_Nexustock/planning/phases/phase_17_rma_return_flow.md) | Hàng trả về, QC phân loại |
| 18 | [Wave picking](file:///d:/1_Project/48_Nexustock/planning/phases/phase_18_wave_picking.md) | Gom đơn xuất |
| 19 | [Material genealogy](file:///d:/1_Project/48_Nexustock/planning/phases/phase_19_material_genealogy.md) | Cây Lot cha/con, khoanh vùng lỗi |

---

## Stage 3: Enterprise integration

| Phase | Tài liệu | Deliverable |
|---|---|---|
| 20 | [Local Agent foundation](file:///d:/1_Project/48_Nexustock/planning/phases/phase_20_local_agent_foundation.md) | Windows service, WebSocket local, device health |
| 21 | [Scale integration](file:///d:/1_Project/48_Nexustock/planning/phases/phase_21_scale_integration.md) | Cân điện tử, fallback cân tay |
| 22 | [Label printing](file:///d:/1_Project/48_Nexustock/planning/phases/phase_22_label_printing.md) | ZPL/TSPL, print log, reprint |
| 23 | [ERP/WMS legacy contract](file:///d:/1_Project/48_Nexustock/planning/phases/phase_23_erp_wms_legacy_contract.md) | API contract, import/export |
| 24 | [Webhook & integration reliability](file:///d:/1_Project/48_Nexustock/planning/phases/phase_24_webhook_integration_reliability.md) | Retry, idempotency, dead-letter |
| 25 | [Operational observability](file:///d:/1_Project/48_Nexustock/planning/phases/phase_25_operational_observability.md) | Audit, timeline, KPI, alert, trace ID |
| 26 | [Production deployment](file:///d:/1_Project/48_Nexustock/planning/phases/phase_26_production_deployment.md) | Docker, health check, backup, rollback |

---

## Stage 4: Optimization & automation

| Phase | Tài liệu | Deliverable |
|---|---|---|
| 27 | [Cross-docking](file:///d:/1_Project/48_Nexustock/planning/phases/phase_27_cross_docking.md) | Chuyển tiếp trực tiếp hàng vừa nhận sang đơn xuất |
| 28 | [Labor tracking](file:///d:/1_Project/48_Nexustock/planning/phases/phase_28_labor_tracking.md) | Đo năng suất theo task |
| 29 | [Task interleaving](file:///d:/1_Project/48_Nexustock/planning/phases/phase_29_task_interleaving.md) | Gợi ý task kế tiếp |
| 30 | [Hardening & production acceptance](file:///d:/1_Project/48_Nexustock/planning/phases/phase_30_hardening_production_acceptance.md) | Security, load, UAT, cutover, rollback rehearsal |

---

## Tài liệu phase cũ

Các phase cũ đã được chuyển vào thư mục tạm:

[planning/phases/temp](file:///d:/1_Project/48_Nexustock/planning/phases/temp)
