# UAT Signoff — Phase 37 L3 Pilot

**Tenant pack:** DEMO-GENERIC (logical · tenant `…0001`)  
**API:** `http://localhost:5024/api`  
**Ngày:** 2026-07-22  
**Smoke:** `tests/verify_l3_pilot_smoke.ps1` → **PASS 12 / FAIL 0 / SKIP 2** · `verify_l3_results.json`  
**Shipment demo:** `SO-DEMO-155646988`

| ID | Scenario | Status | TraceId / Evidence | Note |
|---|---|---|---|---|
| L3-UAT-01 | Nhận hàng tạo Lot | **PASS** | smoke C · HAPPY-155646988 | |
| L3-UAT-02 | QC Hold chặn move | **PASS** | smoke H · `QC_LOT_ON_HOLD` | Lot-HOLD riêng |
| L3-UAT-03 | QC Release + move | **PASS** | smoke D · Move OK | LOC-L3-DEST |
| L3-UAT-04 | Generate-picks | **PASS** | smoke E · pickTaskCount=1 | P36 engine |
| L3-UAT-05 | Complete pick | **PASS** | smoke F | |
| L3-UAT-06 | Insufficient available | **PASS** | smoke J · `INSUFFICIENT_QTY` | |
| L3-UAT-07 | Offline MOVE vs reserved | **PASS*** | verify_l2 DF-01 disk + UAT-06 parity | Offline sync SKIP response shape |
| L3-UAT-08 | Tenant isolation | **PASS** | smoke I · tenant `…0002` | |

**Verdict UAT:** **PASS*** (điều kiện)

**Điều kiện:**
1. Pack complete SKIP — `WEIGHT_SOURCE_INVALID` (scale governance; không block L3 generic).  
2. Offline UAT-07 PASS* — DF-01 đã đóng P36 + available check online UAT-06.  
3. Rollback rehearsal PASS* `RESTORE_SKIPPED_SAFE` (docker CLI không có trên PATH execute host).

| Vai trò | Ký | Ngày |
|---|---|---|
| JARVIS (kỹ thuật) | **PASS*** smoke 12/0 · freeze/unfreeze 200 | 2026-07-22 |
| FOUNDER | ☒ Chấp nhận PASS* · ☐ Yêu cầu restore/pack thật | 2026-08-02 |
