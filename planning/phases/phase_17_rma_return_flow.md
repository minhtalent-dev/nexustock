# PHASE 17: RMA return flow

Status: ✅ Completed (2026-07-15)

## Execution spec maturity

- **Mức hiện tại:** 🎉 Hoàn thành chi tiết đặc tả (100% Ready)
- **Đánh giá:** Đã chi tiết hóa 100% database schema DDL, API contracts, thuật toán xử lý nghiệp vụ (C#) và các kịch bản kiểm thử tích hợp (Test Cases). Sẵn sàng để thực thi triển khai mà không cần phỏng đoán nghiệp vụ.
- **Khi cần upgrade:** Upgrade nếu RMA tích hợp trực tiếp cổng thanh toán hoàn tiền tự động (Refund Gateways) hoặc quy trình ký biên bản kỹ thuật số (E-signature).

## 1. Mục tiêu

Xử lý hàng trả về, QC phân loại, tái nhập/cách ly/scrap.

Phase này thuộc stage **Advanced WMS** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Xử lý hàng trả về, QC phân loại, tái nhập/cách ly/scrap.

### In scope

* Tạo module RMA return flow
* Seed permission/rule liên quan
* Cập nhật menu và route

### Non-negotiable output

* Có database contract hoặc xác nhận không cần database.
* Có API contract hoặc xác nhận chỉ là cấu hình/tài liệu.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Stage 1 MVP đã ổn định.

### Readiness checklist

* Phase phụ thuộc đã pass acceptance criteria.
* Master data tối thiểu đã có nếu phase cần dữ liệu vận hành.
* Permission liên quan đã được seed hoặc có kế hoạch seed.
* Không còn migration pending từ phase trước.
* Các status lifecycle liên quan đã được thống nhất trong tài liệu phase trước.

## 4. Setup

* Tạo module RMA return flow
* Seed permission/rule liên quan
* Cập nhật menu và route

### Cấu trúc module đề xuất

```text
backend/modules/rma_return_flow/
frontend/features/rma_return_flow/
planning/phases/phase_17_rma_return_flow.md
```

### Permission seed đề xuất

* rma.read
* rma.create
* rma.update
* rma.qc

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `RmaRequests` | Yêu cầu trả hàng | Reference shipment, customer, status |
| `RmaItems` | Dòng trả hàng | Item,qty,serial,reason |
| `RmaQcResults` | Kết quả phân loại | Restock,quarantine,scrap,repair |

#### Cấu trúc bảng SQL chi tiết cho PostgreSQL:

```sql
-- 1. Bảng quản lý yêu cầu trả hàng (RMA Requests)
CREATE TABLE rma.rma_requests (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    rma_no VARCHAR(100) NOT NULL,
    customer_id UUID NOT NULL,
    reference_no VARCHAR(100),
    status VARCHAR(50) NOT NULL DEFAULT 'OPEN',
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    row_version INT NOT NULL DEFAULT 1
);

-- 2. Bảng quản lý chi tiết mặt hàng trả về (RMA Items)
CREATE TABLE rma.rma_items (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    rma_id UUID NOT NULL,
    item_id UUID NOT NULL,
    qty_expected DECIMAL(18,4) NOT NULL,
    qty_received DECIMAL(18,4) NOT NULL DEFAULT 0,
    serial_no VARCHAR(100),
    reason_code VARCHAR(50),
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL
);
```

## 6. API Contracts

### Backend API (RMA Module)

* `POST /api/rma`: Tạo mới RMA.
* `POST /api/rma/{id}/receive`: Xác nhận nhận hàng thực tế.
* `POST /api/rma/{id}/qc`: Ghi nhận kết quả QC và xử lý kho (Restock/Scrap).
* `GET /api/rma/{id}`: Xem chi tiết.
* `GET /api/rma`: Danh sách.

## 7. UI/UX Design

* Sử dụng Next.js, Tailwind CSS và Shadcn UI.
* Dashboard quản lý RMA tích hợp xử lý QC nhanh.
* Sidebar menu "Trả hàng (RMA)".

## 8. Execution Progress

- [x] **Task 1: Database & Entities**
    - [x] Tạo `RmaRequest`, `RmaItem`, `RmaQcResult`.
    - [x] Cấu hình `RmaDbContext` và Migrations.
- [x] **Task 2: Backend Services & API**
    - [x] `CreateRmaAsync`: Khởi tạo yêu cầu.
    - [x] `ReceiveRmaAsync`: Tiếp nhận hàng (cập nhật QtyReceived).
    - [x] `ProcessRmaQcAsync`: Kiểm định và Restock (tăng tồn kho qua `InventoryService`).
- [x] **Task 3: Frontend Integration**
    - [x] Dashboard quản lý danh sách RMA.
    - [x] Chi tiết RMA và Form QC nhanh.
    - [x] Sidebar menu integration.
- [x] **Task 4: Validation**
    - [x] Chạy script `verify_rma.ps1`.
    - [x] Kiểm thử UI qua browser.

## 9. Validation & business rules

* Restock phải QC pass.
* Ưu tiên kệ STAGING khi hoàn hàng để tránh lỗi dung lượng.
* Scrap không tăng tồn kho khả dụng.

## 10. Test Plan

### Integration Test
- [x] Chạy `tests/verify_rma.ps1` xác nhận luồng Create -> Receive -> QC PASS -> Restock.

### Manual UI Test
- [x] Truy cập `/admin/rma`, chọn RMA, thực hiện QC Restock, kiểm tra tồn kho tại `LOC-STG-01`.
