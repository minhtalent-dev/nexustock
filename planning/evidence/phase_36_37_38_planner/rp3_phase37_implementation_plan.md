# OBJECTIVE

Phase 37 L3 Customer Pilot: evidence + seed DEMO + cutover/rollback docs + `verify_l3_pilot_smoke.ps1` + UAT signoff → `PILOT_READY*`.

**Maturity:** `rp1` 100% Ready · `rp2` EP atomic · **`rp3` PASS** (0 blind spot block) — chờ FOUNDER Proceed.

SoT: `d:\1_Project\48_Nexustock\planning\phases\phase_37_golive_l3_customer_pilot.md` (§20–§22)  
Index: `d:\1_Project\48_Nexustock\planning\function_index_phase37_l3_pilot.md`

# USER REVIEW REQUIRED

> [!IMPORTANT]
> 1. Proceed P37 trước `/18-auto-execute`.
> 2. Port: `$env:NEXUSTOCK_API_URL` hoặc `http://localhost:5024/api`.
> 3. EP3 restore: `ALLOW_RESTORE_TO_TARGET=true` chỉ DB pilot/local.
> 4. Pack SKIP OK; EP1 **reuse products** (không bắt buộc create Product API).
> 5. AC-08 SAP vẫn WAIVED.
> 6. EP5: **Lot-HOLD riêng** — xem phase_37 §22.1.

# OPEN QUESTIONS

> [!NOTE]
> 0 câu hỏi block. OOS §20.5 + OOS-05 EP4 thời gian tay.

# ARCHITECTURE OVERVIEW

- **Current:** P36 CLOSED; P30 readiness APIs sống; thiếu pilot evidence pack + verify_l3.
- **Target:** Docs + seed PS1 + smoke PS1 + UAT signed; **0** module C# mới.
- **Reuse:** P26 backup · P30 freeze/UatRun · P36 generate-picks.

# EXECUTION PHASES

## EP0 — Evidence scaffold
- **Goal:** Thư mục + template markdown/json
- **Risk:** LOW
- **Primary:** `d:\1_Project\48_Nexustock\planning\evidence\phase_37\`
- **Steps:**
  1. `shots/.gitkeep`
  2. `uat_signoff.md` — bảng L3-UAT-01…08 Status/TraceId/Note + chữ ký
  3. Skeletons: cutover / rollback / hypercare
  4. `ac_pack_status.json` — schema §12; `l2P0":"CLOSED"`; uat pending
- **MUST NOT:** Xóa evidence P30
- **Validation:** 6 file + shots
- **Continuation:** EP1

## EP1 — Seed DEMO-GENERIC (API)
- **Goal:** `tests/seed/demo_generic_tenant.ps1`
- **Risk:** MEDIUM
- **Primary:** `d:\1_Project\48_Nexustock\tests\seed\demo_generic_tenant.ps1`
- **Steps:**
  1. `$API_URL` env hoặc `http://localhost:5024/api`
  2. Login admin
  3. Ensure `LOC-SORT-01` capacity cao
  4. **REUSE** ≥5 product active non-serial (BS-R3-05) — ghi ids vào `seed_summary.json`
  5. Partners reuse; PO/SO naming `PO-DEMO-*` / `SO-DEMO-*` khi smoke tạo
  6. Idempotent exit 0
- **MUST NOT:** Bắt buộc UpsertProduct phức tạp; create-tenant API
- **Validation:** seed_summary có productIds + locationId
- **Continuation:** EP5

## EP2 — Cutover runbook pilot
- **Goal:** `cutover_runbook_pilot.md`
- **Risk:** LOW
- **Steps:** T-7→T+3; freeze API; nếu `CUTOVER_FREEZE_DENIED` → SKIP documented (BS-R3-08)
- **Continuation:** EP3

## EP3 — Rollback rehearsal
- **Goal:** RTO phút hoặc PASS* `RESTORE_SKIPPED_SAFE`
- **Risk:** MEDIUM
- **Steps:** Windows `docker exec … pg_dump` (index §H); **không** restore prod
- **Continuation:** EP4

## EP4 — Manual UAT
- **Goal:** Checklist + shots
- **Risk:** MEDIUM
- **Steps:** Cite TraceId từ EP5 cho UAT auto-covered; shot FE; UAT-07 movement/offline
- **Validation:** 8 dòng status trong `uat_signoff.md`
- **Continuation:** EP6

## EP5 — verify_l3_pilot_smoke.ps1
- **Goal:** Gate tự động FAIL=0
- **Risk:** HIGH
- **Primary:** `d:\1_Project\48_Nexustock\tests\verify_l3_pilot_smoke.ps1`
- **Steps:** **Đúng thứ tự phase_37 §22.1 A–L** (Lot-HOLD riêng; Register→Login; không `$pid`)
- **Copy payloads:** §22.2 Hold/Move · §22.3 UAT-08 · CompletePick `{pickedQty}` · Move `reasonCode=TEST_SEED`
- **Pack try body:** `{ "packageNo":"PKG-L3-1", "weight":1.0, "weightSource":"manual", "scaleStable":true }` → fail = SKIP
- **Offline optional:** `POST /api/mobile/offline-sync` operations `[{ clientOperationId, stepType:"MOVE", payload:"{...MovePayload...}" }]`
- **Validation:** exit 0; write `verify_l3_results.json`; gọi `verify_l2_p0_integrity.ps1`
- **Failure Recovery:** Fix script only (hotfix P0 nếu regress)
- **Continuation:** EP4 rồi EP6

## EP6 — Signoff + close
- **Goal:** `PILOT_READY` | `PILOT_READY_CONDITIONAL`
- **Dependencies:** EP5 PASS + EP3 RTO/PASS*
- **Steps:** ac_pack_status · optional uat-runs · FOUNDER ký · IMPLEMENTATION_PLAN ✅
- **Continuation:** DONE

# TEST PLAN SUMMARY

| Test | Command |
|---|---|
| Seed | `powershell -File tests/seed/demo_generic_tenant.ps1` |
| L3 smoke | `powershell -File tests/verify_l3_pilot_smoke.ps1` |
| L2 regress | `powershell -File tests/verify_l2_p0_integrity.ps1` |
| Allocation | `powershell -File tests/verify_allocation.ps1` |

# ROLLBACK STRATEGY

1. Docs-only → git revert docs.  
2. DB: restore backup EP3.  
3. Unfreeze nếu quên.

# FINAL VALIDATION CHECKLIST

- [ ] EP0 evidence scaffold
- [ ] EP1 DEMO seed (reuse products)
- [ ] EP2 cutover runbook
- [ ] EP3 rollback RTO / PASS*
- [ ] EP4 UAT 01–08
- [ ] EP5 verify_l3 PASS + l2 regress
- [ ] EP6 FOUNDER signoff · PILOT_READY*

# DEFINITION OF DONE

phase_37 §14 + §22 BS-R3 = 0 open block + EP0–EP6 done.

# rp3 TRACE

Blind spots đóng: BS-R3-01…18 — xem phase_37 §22. Critic residual 0 block.
