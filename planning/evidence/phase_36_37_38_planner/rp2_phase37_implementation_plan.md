# OBJECTIVE

Phase 37 L3 Customer Pilot: evidence + seed DEMO + cutover/rollback docs + `verify_l3_pilot_smoke.ps1` + UAT signoff → `PILOT_READY*`.

**Maturity:** `rp1` 100% Ready · **`rp2` EP atomic + function index** (critic **9.5**) — chờ FOUNDER Proceed.

SoT: `d:\1_Project\48_Nexustock\planning\phases\phase_37_golive_l3_customer_pilot.md` (§20–§21)  
Index: `d:\1_Project\48_Nexustock\planning\function_index_phase37_l3_pilot.md`

# USER REVIEW REQUIRED

> [!IMPORTANT]
> 1. Proceed P37 trước `/18-auto-execute`.
> 2. Port: `$env:NEXUSTOCK_API_URL` hoặc `http://localhost:5024/api`.
> 3. EP3 restore: `ALLOW_RESTORE_TO_TARGET=true` chỉ trên DB pilot/local — **không** production.
> 4. Pack step có thể SKIP nếu weight/scale fail — ghi SKIP trong results.
> 5. AC-08 SAP vẫn WAIVED.

# OPEN QUESTIONS

> [!NOTE]
> 0 block. OOS: staging remote, Operator seed full, AC-06 5k bench, P38.

# ARCHITECTURE OVERVIEW

- **Current:** P36 CLOSED; P30 readiness APIs sống; thiếu pilot evidence pack + verify_l3.
- **Target:** Docs + seed PS1 + smoke PS1 + UAT signed; **0** module C# mới.
- **Reuse:** P26 backup scripts · P30 freeze/UatRun · P36 generate-picks.

# EXECUTION PHASES

## EP0 — Evidence scaffold
- **Goal:** Thư mục + template markdown/json
- **Risk:** LOW
- **Primary:** `d:\1_Project\48_Nexustock\planning\evidence\phase_37\`
- **Steps:**
  1. Tạo `shots/` empty (`.gitkeep`).
  2. `uat_signoff.md` — bảng L3-UAT-01…08 Status/TraceId/Note + chữ ký FOUNDER.
  3. `cutover_runbook_pilot.md` — skeleton T-7…T+3 (điền EP2).
  4. `rollback_rehearsal.md` — skeleton RTO (điền EP3).
  5. `hypercare.md` — Sev-1/2/3 + channel + owner placeholder.
  6. `ac_pack_status.json` — copy schema phase_37 §12; `l2P0=CLOSED`; uat pending.
- **MUST NOT:** Xóa evidence P30.
- **Validation:** 6 file + shots dir tồn tại.
- **Continuation:** EP1

## EP1 — Seed DEMO-GENERIC (API)
- **Goal:** `tests/seed/demo_generic_tenant.ps1`
- **Risk:** MEDIUM
- **Primary:** `d:\1_Project\48_Nexustock\tests\seed\demo_generic_tenant.ps1`
- **Steps:**
  1. `$API_URL` = env hoặc `http://localhost:5024/api`.
  2. Login `admin@nexustock.com` / `AdminSecret123!`.
  3. Ensure `LOC-SORT-01` capacity cao (copy verify_l2_p0).
  4. Tạo ≥5 products `DEMO-SKU-{suffix}` (non-serial) nếu chưa có (GET filter code).
  5. Partners reuse; optional warehouse note `WH-DEMO`.
  6. Output JSON summary path ids → `planning/evidence/phase_37/seed_summary.json`.
  7. Idempotent: re-run không fail hard nếu code đã tồn tại.
- **MUST NOT:** SQL raw bắt buộc; create-tenant API.
- **Validation:** seed script exit 0; ≥1 DEMO-SKU trong master-data.
- **Continuation:** EP5 (smoke sớm) hoặc EP2

## EP2 — Cutover runbook pilot
- **Goal:** `cutover_runbook_pilot.md` đầy đủ
- **Risk:** LOW
- **Steps:**
  1. Timeline T-7→T+3 từ phase_37 §9.
  2. Map API: `POST /api/admin/cutover/freeze|unfreeze` + flag `FF_CUTOVER_FREEZE_ENABLED`.
  3. Smoke note: bật flag (nếu tắt → ghi SKIP freeze + lý do).
  4. Hypercare channel placeholder (Teams/Email).
- **Validation:** Doc ≥ T-7, T0, T+3; có freeze section.
- **Continuation:** EP3

## EP3 — Rollback rehearsal
- **Goal:** `rollback_rehearsal.md` có **RTO phút** thật
- **Risk:** MEDIUM (ops)
- **Steps:**
  1. Backup: ưu tiên `scripts/db-backup.sh` trong container; Windows → function_index §H `docker exec pg_dump`.
  2. Ghi timestamp start/end restore (có thể dry-run restore vào DB phụ hoặc restore local với `ALLOW_RESTORE_TO_TARGET=true` **chỉ** khi FOUNDER/local đồng ý).
  3. Nếu không restore phá DB: **PASS*** với backup-only + ước lượng RTO từ thời gian dump + note `RESTORE_SKIPPED_SAFE` — FOUNDER chấp nhận trong signoff.
  4. Điền `rtoMinutes` vào `ac_pack_status.json`.
- **MUST NOT:** Restore production không có ALLOW.
- **Validation:** File có số RTO hoặc PASS* documented.
- **Continuation:** EP4

## EP4 — Manual UAT L3-UAT-01…08
- **Goal:** Checklist + shots
- **Risk:** MEDIUM
- **Steps:**
  1. Theo function_index §G + FE routes §8 phase_37.
  2. Mỗi UAT: TraceId + PASS/FAIL + shot nếu UI.
  3. UAT-07: `/mobile/movement` hoặc API offline-sync.
  4. UAT-08: Register tenant `00000000-0000-0000-0000-000000000002`.
- **Validation:** `uat_signoff.md` đủ 8 dòng status.
- **Continuation:** EP6 (sau EP5)

## EP5 — verify_l3_pilot_smoke.ps1
- **Goal:** Gate tự động
- **Risk:** HIGH
- **Primary:** `d:\1_Project\48_Nexustock\tests\verify_l3_pilot_smoke.ps1`
- **Steps (bắt buộc §20.4):**
  1. Assert helpers như verify_l2_p0; **không** `$pid`.
  2. Login → optional `& demo_generic_tenant.ps1`.
  3. Inbound+QC Release (copy l2).
  4. Move OK với `reasonCode` (vd `ADJ-DEMO`).
  5. Shipment + generate-picks → `pickTaskCount -gt 0`.
  6. Complete pick `{ pickedQty }` = full qty.
  7. Pack: try complete; on fail → Assert SKIP ghi note (weight).
  8. Hold lot → move expect `QC_LOT_ON_HOLD`.
  9. Register user B tenant `...0002` → GET `/outbound/shipments` không chứa shipmentNo DEMO của A (hoặc empty/403).
  10. Gọi `verify_l2_p0_integrity.ps1` regression.
  11. Write `verify_l3_results.json` pass/fail counts.
- **Validation:** exit 0; FAIL=0 (SKIP pack OK).
- **Failure Recovery:** Fix script; không đổi backend trừ hotfix P0.
- **Continuation:** EP4 nếu chưa; rồi EP6

## EP6 — Signoff + close docs
- **Goal:** `PILOT_READY` hoặc `PILOT_READY_CONDITIONAL`
- **Steps:**
  1. Update `ac_pack_status.json` (uat counts, rollback, ac08 WAIVED).
  2. Optional `POST /api/admin/readiness/uat-runs` + signoff.
  3. FOUNDER ký `uat_signoff.md`.
  4. IMPLEMENTATION_PLAN row P37 ✅ + phase_37 maturity DoD.
  5. Brain task_tracking all `[x]`.
- **Continuation:** DONE → P38 optional

# TEST PLAN SUMMARY

| Test | Command |
|---|---|
| Seed | `powershell -File tests/seed/demo_generic_tenant.ps1` |
| L3 smoke | `powershell -File tests/verify_l3_pilot_smoke.ps1` |
| L2 regress | `powershell -File tests/verify_l2_p0_integrity.ps1` |
| Allocation | `powershell -File tests/verify_allocation.ps1` |

# ROLLBACK STRATEGY

1. Không deploy code mới → git N/A cho docs-only.  
2. DB: restore backup EP3.  
3. Unfreeze cutover nếu quên.

# FINAL VALIDATION CHECKLIST

- [ ] EP0 evidence scaffold
- [ ] EP1 DEMO seed script
- [ ] EP2 cutover runbook
- [ ] EP3 rollback RTO / PASS*
- [ ] EP4 UAT 01–08
- [ ] EP5 verify_l3 PASS + l2 regress
- [ ] EP6 FOUNDER signoff · PILOT_READY*

# DEFINITION OF DONE

phase_37 §14 + function_index MUST NOT = 0 violation.

# rp2 TRACE

Critic 9.5 — xem `critic_report.md`. Refine: EP3 PASS* path; pack SKIP; Windows pg_dump.
