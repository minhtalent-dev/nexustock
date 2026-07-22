# Walkthrough — `/30-auto-project-planner` P36 → P37 → P38

**Ngày:** 2026-07-22  
**Agent:** JARVIS  
**INPUT FOUNDER:** P36 (L2-P0) → P37 (L3) → [P38 UI theo AUDIT_UI] + `/30-auto-project-planner`

---

## Tóm tắt

Đã sinh **3 phase spec 95% Execution-Ready**, cập nhật roadmap 35→**38**, neo SoT L2 + UI Option B.

| Phase | File | Maturity | Critical Path |
|---|---|---|---|
| 36 Inventory Integrity (L2-P0) | `planning/phases/phase_36_inventory_integrity_l2_p0.md` | **95%** | Có |
| 37 Go-Live L3 Customer Pilot | `planning/phases/phase_37_golive_l3_customer_pilot.md` | **95%** | Có (sau 36) |
| 38 UI Design System Pass | `planning/phases/phase_38_ui_design_system_pass.md` | **95%** | Không (trừ FOUNDER chốt đẹp-trước-bán) |

---

## Rủi ro đã khóa trong spec

| Rủi ro | Phase | Giải pháp trong spec |
|---|---|---|
| 2 engine allocation | 36 | GeneratePicks → AllocateAsync + PickTaskMaterializer |
| Âm kho / reserved lệch | 36 | Interceptor + CHECK SQL |
| Offline cướp reserved | 36 | DF-01 available formula |
| UAT lẫn Sharp/SAP | 37 | Generic UAT 01–08; AC-08 waived |
| UI phình scope | 38 | Option B + 7 waves + verify hardcode |

---

## Auto-critique

Cả 3 phase đã trả lời 4 câu Write concurrency / Hardware / Network / Third-party trong §18 từng file → **95%**.

---

## Proceed (FOUNDER)

1. ☐ **Proceed Phase 36** (bắt buộc trước)  
2. ☐ Proceed Phase 37 sau DoD 36  
3. ☐ Proceed Phase 38 (sau/song song muộn)

**Không code** đến khi FOUNDER ký Proceed (quy tắc workflow §8.4).

---

## File đã cập nhật

- `planning/IMPLEMENTATION_PLAN.md` — catalog + progress + gantt + deep-spec backlog  
- `planning/ACCEPTANCE_L2_GENERIC_WMS_FOUNDATION.md` — §7 phase map  
- `planning/AUDIT_UI_UX_PROD_READINESS.md` — Option B khóa P38  
- 3 file `planning/phases/phase_36|37|38_*.md`  
- `planning/evidence/phase_36_37_38_planner/walkthrough.md` (file này)
