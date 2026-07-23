# Audit coverage P43–P45 vs Nav + Gap inventory

**At:** 2026-07-23  
**Verdict:** **PASS** — mọi ❌ trong inventory có owner; không sót module nav cần upload/excel mà chưa đề cập.

## Nav → Owner (A=attach, E=excel)

| Nav id | Cần A? | Cần E? | Trạng thái |
|---|---|---|---|
| products | ✅ có | ✅ có | Done P41 |
| uoms | — | ❌ | **P43** |
| warehouses | — | ❌ | **P43** |
| zones | — | ❌ | **P43** |
| locations | — | ✅ có | Done P41 |
| partners | — | ✅ có | Done P41 |
| reasons | — | ❌ | **P43** |
| import | — | hub | **P43** (+4) · **P45** (PACKAGES) |
| inbound | ❌ | ❌ | **P43** A+E · **P45** line import |
| lots | ❌ | ❌ | **P44** |
| qc | ⚠️→panel | — | **P43** |
| putaway | ❌ | ❌ | **P44** |
| outbound | ❌ | ❌ | **P43** |
| allocation | — | thấp | **N/A** (khóa) |
| waves | ❌ | ❌ | **P44** |
| crossDocking | ❌ | ❌ | **P44** |
| rma | ❌ | ❌ | **P43** |
| inventory | — | ❌ | **P44** E |
| stocktakes | ❌ | ❌ | **P43** · line count **P45 P1** |
| exceptions | ❌ | ❌ | **P44** |
| replenishment | — | ❌ | **P44** E (không A — by design) |
| lpn | ❌ | ❌ | **P44** |
| serial | — | ✅ CSV | Done P16 |
| genealogy | — | — | **N/A** |
| labor / laborSessions | — | — | **N/A** |
| taskInterleaving | — | — | **N/A** |
| integration* | — | ✅ CSV | Done P23 |
| webhooks | — | — | **N/A** |
| users/roles/rules/audit | — | — | **N/A** |
| fileStorage | hub | — | Done P41–42 |
| localAgent / obs / readiness / cutover | — | — | **N/A** |
| mobile RF | ❌ camera | — | **P45** |
| Package (no nav) | — | ❌ | **P45** |
| Thumbnail/URL | ❌ polish | — | **P45** (OCR optional) |

## ❌ inventory → Phase

| Gaps | Phase |
|---|---|
| #2–4,7–9,11,13,17,19 | P43 |
| #10,12,15,16,18,20–22 | P44 |
| #31–34 | P45 |
| #14 | N/A |
| #1,5,6,23–30 | Done / N/A |

## Caveat (không phải sót module)

1. **#34 OCR** — P45 DoD bắt buộc thumb + URL; OCR = optional/skip có ghi chú.  
2. **Replenishment** — chỉ export, không attach (không có bằng chứng vật lý điển hình).  
3. **Allocation** — không A/E (N/A đã khóa).  
4. **Stocktake line import** — P45 P1 (ASN line = P0).

## Kết luận

**Không còn module/function nghiệp vụ cần upload hoặc excel mà chưa được đề cập đúng chủ** trong chương trình P43–P45.
