# Cutover Runbook — Pilot (Phase 37)

**T0 = go-live pilot** · Tham chiếu P26/P30 · Không SAP.

## Timeline

| Mốc | Việc | Owner | API / Script |
|---|---|---|---|
| **T-7** | Freeze feature (chỉ hotfix P36 regress) | Dev | — |
| **T-5** | Backup staging/local + dry-run restore note | DevOps | `scripts/db-backup.sh` hoặc `docker exec … pg_dump` |
| **T-3** | UAT signoff signed | FOUNDER | `uat_signoff.md` |
| **T-1** | Final backup; announce hypercare channel | DevOps | backup + `hypercare.md` |
| **T0** | Enable pilot users; smoke `verify_l3_pilot_smoke.ps1` | Dev | API `:5024` |
| **T+1…T+3** | Hypercare daily severity review | Dev + FOUNDER | `hypercare.md` |

## Freeze / Unfreeze (P30)

```http
POST /api/admin/cutover/freeze
Authorization: Bearer {admin}
{ "reason": "P37 pilot T0 freeze" }

POST /api/admin/cutover/unfreeze
{ "reason": "P37 pilot reopen" }
```

- Flag: `FF_CUTOVER_FREEZE_ENABLED` (seed mặc định Enabled).  
- Permission: `readiness.cutover.freeze`.  
## Freeze smoke (2026-07-22 `/18-auto-execute`)

| Call | Result |
|---|---|
| `POST /api/admin/cutover/freeze` | **200** `isFrozen=true` |
| `POST /api/admin/cutover/unfreeze` | **200** |

Flag `FF_CUTOVER_FREEZE_ENABLED` hoạt động — **không SKIP**.

```powershell
powershell -File tests/verify_l3_pilot_smoke.ps1
powershell -File tests/verify_l2_p0_integrity.ps1
```

## Rollback trigger

Sev-1 không khắc phục trong 2h → restore theo `rollback_rehearsal.md`.
