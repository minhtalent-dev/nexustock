# Phase 30 evidence

Thư mục chứa bằng chứng nghiệm thu Readiness Gate (UAT, rollback, load, security, ERP).

## FOUNDER decisions (2026-07-21)

- AC-01: **A** — Upstream Critical/High issues = 0.
- AC-08: SAP sandbox **chưa sẵn** → **waiver** (SKIP/defer ERP contract 5-case đến khi sandbox sẵn; go-live ERP integration không được coi đã nghiệm thu production cho đến khi AC-08 PASS).

## Evidence checklist (điền khi chạy)

| AC | File / artifact | Status |
|---|---|---|
| AC-01 | issue-board-clean.png | Pending |
| AC-02 | rollback-rehearsal.* | Pending |
| AC-03 | backup-restore-rpo.* | Pending |
| AC-04 | uat-signoff.* | Pending |
| AC-05 | load-50-rf.* | Pending |
| AC-06 | allocation-5k.* | Pending |
| AC-07 | security-audit.* | Pending |
| AC-08 | **WAIVED** — xem WAIVER_AC08.md | Waived |
| AC-09 | observability-trace.* | Pending |
| AC-10 | feature-flag-toggle.* | Pending |
| AC-11 | db-constraints.* | Pending |
| AC-12 | msix-signtool.* (SKIP rehearsal nếu chưa cert) | Pending |
| AC-13 | cutover-runbook-signed.* | Pending |
| AC-14 | gitleaks.* | Pending |
