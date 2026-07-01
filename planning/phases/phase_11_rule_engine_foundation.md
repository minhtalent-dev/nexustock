# PHASE 11: Rule engine foundation

## 1. Mục tiêu

Rule set, condition, action, priority, execution log.

## 2. Phạm vi

Triển khai đúng deliverable của phase này, không gom thêm chức năng ngoài phạm vi.

## 3. Điều kiện đầu vào

Các phase phụ thuộc đã hoàn tất và dữ liệu nền liên quan đã sẵn sàng.

## 4. Setup

Tạo module, permission, cấu hình môi trường và dữ liệu seed tối thiểu cho phase.

## 5. Database

Tạo hoặc cập nhật bảng cần thiết, index, constraint, transaction boundary và migration tương ứng.

## 6. Backend/API

Tạo API CRUD/command/query cần thiết, validate input, kiểm quyền, transaction và error response chuẩn.

## 7. Frontend/RF/mobile

Tạo màn hình web hoặc mobile/RF cần thiết, trạng thái loading/error/empty, không dùng inline style.

## 8. Execution flow

Mô tả luồng người dùng từ đầu vào đến khi hoàn tất giao dịch, bao gồm bước xác nhận quan trọng.

## 9. Validation & business rules

Áp dụng rule nghiệp vụ, chống dữ liệu trùng, chống sai trạng thái và bảo vệ tồn kho khỏi lệch số.

## 10. Exception handling

Chuẩn hóa lỗi người dùng, lỗi dữ liệu, lỗi quyền, lỗi đồng bộ và liên kết exception framework nếu cần.

## 11. Observability

Ghi audit log, activity timeline, trace ID và KPI/alert liên quan nếu phase phát sinh dữ liệu vận hành.

## 12. Test plan

Unit test cho logic chính, integration test cho API/DB transaction, E2E test cho luồng vận hành chính.

## 13. Acceptance criteria

Phase chỉ hoàn tất khi deliverable chạy được end-to-end, test critical pass và dữ liệu sau giao dịch đối soát đúng.

## 14. Out of scope

Không triển khai tối ưu nâng cao hoặc tích hợp ngoài phạm vi phase hiện tại.

## 15. Dependencies

Xem dependency chi tiết tại IMPLEMENTATION_PLAN.md.
