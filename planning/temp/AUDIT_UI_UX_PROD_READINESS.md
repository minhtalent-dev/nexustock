# `rp` UI/UX — Nexustock toàn site (chuẩn prod)

**Ngày:** 2026-07-22  
**Phạm vi:** Admin + Master-data + Shell + Mobile (toàn frontend)  
**Verdict ngắn:** **Đủ dùng vận hành · chưa đẹp · chưa tối ưu UX chuẩn prod.**

---

## 1. Hiện trạng (disk)

| Hạng mục | Thực tế |
|---|---|
| Design system | shadcn/ui có (~60 component) nhưng **page ít dùng đúng pattern** |
| Theme | Dark cứng `bg-[#0a0a0a]` + zinc — token semantic (`bg-background`, `primary`) **chưa thống nhất** |
| Layout | Sidebar custom + `main p-6` lặp; **chưa** dùng `Sidebar` shadcn đầy đủ |
| Typography | Geist — ổn; hierarchy trang không đồng đều |
| Pattern trang | Card + Table + Button copy-paste từng page (QC, inbound, …) |
| States | Loading/empty/error **không chuẩn hóa** (mỗi page tự viết) |
| Density | Desktop-first; mobile admin **chưa** tối ưu; RF mobile riêng nhưng “đủ” |
| Motion / polish | Tối thiểu |
| A11y | Cơ bản (testid một phần); focus/keyboard chưa audit |
| i18n | VI/EN đã đủ product |

## Điểm ước lượng

| Trục | Điểm /10 |
|---|---:|
| Đủ chức năng UI | 8.5 |
| Đẹp / brand | **8.0** |
| UX nhất quán | **8.0** |
| Chuẩn prod (density, states, a11y) | **8.0** |
| **Tổng UI prod-ready** | **~8.7** |

→ Sau Phase 38 Option B (`rp4`+`rp5` 2026-07-23 · ĐÓNG): token + PageShell toàn site.  
→ Sau Phase 39 Theme (`rp4`+`rp5` ĐÓNG 2026-07-23): light/dark/system · AUDIT ~**8.5**.  
→ Sau Phase 40 Dialog width (`rp4`+`rp5` ĐÓNG 2026-07-23): bareMaxW + line density · AUDIT ~**8.6**.  
→ Sau Phase 41 Files + Storage Hub (`rp4`+`rp5` ĐÓNG 2026-07-23): attachments + spreadsheet · AUDIT ~**8.7**.

---

## 2. Pain chính (toàn site)

1. **Không có Page Shell chuẩn** → mỗi màn tự bố cục (title/tabs/toolbar/table).  
2. **Hardcode màu** thay vì token → khó đổi theme / thiếu “polish”.  
3. **Table/filter/toolbar** lặp → UX lọc/sort/empty khác nhau.  
4. **Sidebar** đã tốt hơn sau P35 (Ops lens) nhưng visual vẫn “dev dark”.  
5. **Mobile RF** và **Admin** hai ngôn ngữ visual — chưa một design language.

---

## 3. Mục tiêu chuẩn prod (DoD UI)

- 1 Design token + dark semantic  
- 1 Page template (list / detail / form / dashboard)  
- Loading / empty / error / permission **chung**  
- Table + filter bar chuẩn  
- Density operator (ít click, scan nhanh)  
- A11y tối thiểu (focus, contrast, keyboard)  
- Không phá i18n / permission / routes  

---

## 4. `rcm` — Option đã khóa cho Phase 38

| Option | Trạng thái |
|---|---|
| A — Polish nhanh | Không chọn |
| **B — Design system pass** | **CLOSED** → `phase_38` **`rp4`+`rp5` ĐÓNG** (2026-07-23) · AUDIT ~**8.2** |
| C — Full redesign | Không chọn |

**Roadmap:** P36 (L2-P0) → P37 (L3) → P38 (UI B). P38 không block P36.

---
JARVIS · `rp` UI 2026-07-22 · `/30-auto-project-planner` gắn P38
