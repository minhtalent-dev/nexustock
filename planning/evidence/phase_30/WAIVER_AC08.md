# Waiver AC-08 — ERP contract test (Phase 30)

**Ngày:** 2026-07-21  
**Người quyết định:** FOUNDER  
**Phạm vi:** Acceptance Criteria AC-08 (ERP contract 5 case)

## Quyết định

SAP sandbox **chưa sẵn sàng**. AC-08 được **waiver** cho giai đoạn execute / DoD rehearsal Phase 30.

## Hệ quả bắt buộc

1. `tests/verify_readiness.ps1` và evidence Phase 30 **không** yêu cầu PASS AC-08 để tick module Readiness / cutover / UAT kho.
2. Probe `GET /api/admin/readiness` được phép trả trạng thái SAP = `Unavailable` / `Skipped` mà không fail gate readiness nội bộ.
3. Cutover runbook bước smoke SAP (T-01:00) và hypercare đơn SAP: **defer** hoặc đánh dấu `Skipped — AC-08 waived` cho đến khi sandbox sẵn.
4. **Go-live production có tích hợp ERP thật** chỉ được ký sau khi AC-08 chạy lại và PASS (happy path, missing field, wrong type, duplicate key, oversized payload) trên sandbox hoặc môi trường tương đương.

## Không được hiểu nhầm

- Waiver **không** xóa AC-08 khỏi phase.
- Waiver **không** chứng minh ERP đã sẵn sàng production.
- Khi sandbox sẵn: bỏ waiver này, chạy lại 5 case, lưu evidence vào `planning/evidence/phase_30/ac08-erp-contract.*`.

## Chữ ký

- FOUNDER: xác nhận bằng chat `Sandbox chưa sẵn` + lệnh `` `up `` ngày 2026-07-21.
- Dev/JARVIS: ghi nhận vào phase §19.11 và IMPLEMENTATION_PLAN cùng ngày.
