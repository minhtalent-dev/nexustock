# Measurable acceptance criteria

## Standard

Every phase must prove:

- Functional result works end-to-end.
- Data integrity invariant holds.
- Permission and tenant isolation are enforced.
- Audit or activity timeline exists for mutation.
- Trace ID exists for support.
- Main negative path is tested.
- Rollback or disable path is documented.

## Phase criteria

| Phase | Measurable acceptance criteria |
|---|---|
| 01 | New developer can start API, DB and frontend from README in one pass; `/health/live` returns 200; no committed secret found in config templates. |
| 02 | Demo tenant has item, UOM, warehouse, zone, location and reason code; duplicate code is rejected; inactive master data cannot create new warehouse transaction. |
| 03 | Unauthorized mutation returns 401/403; role permission change writes audit row; API never returns password hash/token secret. |
| 04 | Receive creates lot and RECEIVE ledger row in one transaction; duplicate lot rejected; over-tolerance receive requires permission and reason. |
| 05 | QC hold blocks allocation; release makes lot allocatable; reject requires reason and audit. |
| 06 | Move posts MOVE ledger rows; balance never negative; concurrent move conflict returns 409. |
| 07 | Shipment can allocate, pick, pack and ship; ship posts SHIP ledger; short pick creates exception. |
| 08 | Cycle count locks location; adjustment requires approval; posted adjustment creates immutable ledger row. |
| 09 | RF scan validates workflow context; wrong barcode does not mutate data; scan response under target NFR. |
| 10 | Exception can be opened, assigned, resolved and closed; source entity links back to exception timeline. |
| 11 | Rule priority resolves deterministic result; disabled rule is ignored; execution log records matched rule. |
| 12 | Putaway suggests valid location only; capacity overflow rejected; manual override requires reason. |
| 13 | Allocation reserves only available stock; duplicate allocation idempotent; expired reservation releases stock. |
| 14 | Replenishment creates task when pick face below min; no duplicate open task for same trigger. |
| 15 | LPN move updates all contained balances atomically; mixed lot rules enforced. |
| 16 | Serial number unique per item; shipped serial cannot be reused; serial trace shows lifecycle. |
| 17 | Return can quarantine, release or scrap; returned stock never bypasses QC. |
| 18 | Wave creates grouped pick tasks; cancel wave releases open reservations. |
| 19 | Lot genealogy shows parent/child; affected lot search returns complete recall set. |
| 20 | Agent binds only localhost; pairing/revoke audited; heartbeat visible in UI within 10 seconds. |
| 21 | Stable COM reading emitted only after stable window; manual weight override requires permission/reason. |
| 22 | ZPL and TSPL print jobs are queued; reprint requires reason; print result is auditable. |
| 23 | ERP inbound order import supports preview and commit; duplicate idempotency key returns prior result. |
| 24 | Webhook retries with backoff; max retry moves to DLQ; replay does not duplicate business mutation. |
| 25 | Trace ID links API, job and integration event; KPI dashboard refreshes within documented interval. |
| 26 | Build, backup, migrate, deploy, smoke and rollback checklist completes in rehearsal environment. |
| 27 | Cross-dock suggestion links inbound receipt to outbound demand; manual rejection reason captured. |
| 28 | Worker task timestamps calculate throughput; supervisor can review by user/shift/zone. |
| 29 | Task recommendation respects priority, permission and zone constraints; operator can override with reason. |
| 30 | UAT scripts pass; rollback rehearsal completed; production go/no-go checklist signed. |
