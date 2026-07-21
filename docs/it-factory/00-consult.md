# Consult — Intake / Options / Decision

**Status:** `decided`

## Problem
Phase 31 Localization VI/EN (59/59 pages, 0 backlog) — có nên tách thành nhiều phase nhỏ hơn theo quy tắc /30-auto-project-planner?

## Constraints
- 1 Developer chính
- Mỗi phase 3–7 dev-days
- DoD Phase localization: 59/59 page.tsx + 0 backlog
- Không đổi URL path locale
- Wire errorCode giữ machine EN

## Pain points
- DoD siết làm 1 phase dễ vượt 7 ngày
- Sign-off all-or-nothing trễ giá trị
- Wave đã là ranh giới DoD tự nhiên

## Context
phase_31_localization_vi_en.md đã 95% Ready; inventory FE: admin 41, master-data 8, mobile 7, other 3 (home/login/health-ui) = 59. Wave A–D đã có trong spec.

## Q&A
### q1: Effort thực tế P31 monolithic sau khi siết AC/DoD?
- **Answer:** Ước lượng 8–11 ngày với DoD 59/59 + Errors catalogs đầy đủ + verify inventory/grep.

### q2: Wave A–D đã đủ ranh giới shippable chưa?
- **Answer:** Có — mỗi wave đã có DoD riêng trong §8; có thể map 1:1 hoặc gộp A+B.

### q3: Ưu tiên tốc độ Milestone 5 một lần hay DoD shippable từng bước?
- **Answer:** Ưu tiên deliverable shippable và tuân trần 7 ngày; chấp nhận Milestone 5 lùi sang phase cuối chuỗi localization.

### q4: BE optional errorCodeLabel có cần phase riêng?
- **Assumption:** Không cần phase riêng cho BE Accept-Language — gắn wave D / phase cuối.

## Options
### A — Giữ 1 Phase 31
Giữ 1 Phase 31; 4 wave nội bộ; nhiều PR nhưng chỉ đóng khi 59/59.
**Pros:**
- Một Milestone 5 rõ
- Ít file phase
- Shared foundation không cắt ngang
**Cons:**
- Effort 8–11d vượt trần 7d
- Sign-off muộn all-or-nothing
- Khó báo cáo tiến độ DoD giữa chừng

### B — Tách 3 phase (khuyến nghị)
P31 Foundation+Shell/Admin (Wave A+B, ~41 admin+3 other) · P32 Master-data+WMS core còn lại (Wave C) · P33 Mobile+Errors+đóng 59/59 (Wave D).
**Pros:**
- Mỗi phase ~3–5d trong trần 3–7
- DoD shippable theo cụm page
- Khớp wave; Wave A gộp P31 vì <3d nếu đứng riêng
- P33 khóa AC-09/10 toàn product
**Cons:**
- Milestone 5 đóng ở P33
- Cần thêm phase_32/33 + sync master plan
- Catalog keys tích lũy qua chuỗi

### C — Tách 4 phase = 4 wave
P31A Foundation · P31B Admin · P31C MD/WMS · P31D Mobile/Errors — 4 phase đúng 4 wave.
**Pros:**
- DoD wave = DoD phase 1:1
- Granular tracking
**Cons:**
- P31A ~1d dưới sàn 3d
- 4 lần sign-off overhead
- Phình số phase không cần thiết

## Recommendation
**B** — Sau siết AC/DoD 59/59, monolithic P31 ước 8–11 ngày — vượt trần planner. Tách 3 phase: gộp Wave A vào P31 (foundation quá nhỏ nếu đứng riêng), P32 Wave C, P33 Wave D khóa toàn product. Option C quá mảnh; Option A rủi ro trễ và all-or-nothing.

## Decision
- **Option:** B
- **By:** FOUNDER
- **At:** 2026-07-21T04:59:37.394Z
- **Notes:** Khóa Option B: P31 Foundation+Shell/Admin · P32 Master-data · P33 Mobile+Errors+đóng 59/59. Milestone 5 sau P33.
