# Hypercare — Phase 37 (T+1…T+3)

| Severity | Ví dụ | SLA phản hồi | Owner |
|---|---|---|---|
| Sev-1 | Âm kho / sai reserved hàng loạt | 15 phút | Dev on-call |
| Sev-2 | Không allocate được cả kho | 1 giờ | Dev |
| Sev-3 | UI glitch | 1 ngày | Dev |

**Channel:** ☐ Teams · ☐ Email · ☐ Other: ___________  

**Daily review:** 09:00 local — severity open / closed.

**Rollback trigger:** Sev-1 > 2h không khắc phục → `rollback_rehearsal.md`.
