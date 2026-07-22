# Critic Report — Phase 36 Inventory Integrity (rp2)

**Ngày:** 2026-07-22  
**Đối tượng:** implementation_plan.md (EP0–EP6) + phase_36 + function_index_phase36  
**Thang:** Production readiness critic

---

## Score

| Trục | Điểm | Ghi chú |
|---|---:|---|
| Atomicity EP | 9.5 | EP tách rõ; EP1 HIGH đúng |
| Disk fidelity | 9.7 | Circular ref / CHECK / route đã khóa rp1 |
| Regression safety | 9.3 | Wave CreatePickTasks=false nêu rõ |
| Executor clarity | 9.5 | Path tuyệt đối + MUST NOT |
| **Tổng** | **9.5 / 10** | Ready execute sau Proceed |

---

## Findings

| ID | Sev | Finding | Resolve |
|---|---|---|---|
| C-01 | HIGH | Duplicate `GeneratePicks` nếu thêm controller trước khi xóa cũ | EP1: cùng PR / cùng commit xóa+add |
| C-02 | MED | `AllocateAsync` Commit rồi controller đếm PickTask — OK nếu CreatePickTasks trong TX trước Commit | EP0 comment + code review gate |
| C-03 | MED | Interceptor Singleton + scoped DbContext — OK pattern EF | EP2 dùng GetRequiredService trong factory options |
| C-04 | LOW | Status `Cancelled` trên PickTask có thể chưa dùng | Giữ điều kiện `!= Cancelled` harmeless |
| C-05 | LOW | verify cần API live + port | USER REVIEW port 5024 |
| C-06 | INFO | Lock behavior siết hơn | USER REVIEW — không đổi SoT |

**Blocker mở:** 0 (sau refine).

---

## Refine actions applied

1. EP1 nhấn **cùng PR** xóa OutboundController.GeneratePicks.  
2. EP0 nhấn PickTask add **trước** SaveChanges/Commit.  
3. Function index §F MUST NOT Wave/FE/circular.  
4. Phase_36 §21 gắn EP0–EP6 + link brain + function index.

---

## Verdict

**PASS — 9.5/10** · Spec + plan đủ cho `/18-auto-execute` sau FOUNDER Proceed.
