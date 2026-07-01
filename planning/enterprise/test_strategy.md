# Test strategy (Chiến lược kiểm thử toàn diện dự án Nexustock WMS)

Tài liệu định nghĩa khung kiểm thử từ lập trình local đến môi trường staging/production, tối ưu hóa nguồn lực cho mô hình **1 Developer chính** có sự hỗ trợ của các kịch bản tự động hóa và test data mẫu.

---

## 1. Tháp kiểm thử (Test Pyramid)

Để phát hiện lỗi sớm và giảm tải việc test tay lặp lại, dự án áp dụng mô hình phân bổ nỗ lực kiểm thử sau:

```text
       / \
      / E \      E2E Tests (10%) - Quét luồng RF cầm tay, UI đóng gói, in nhãn.
     /--=--\
    /  Int  \    Integration Tests (30%) - API + DB Transaction + Rules + Auth.
   /---------\
  /   Unit    \  Unit Tests (60%) - Thuật toán FEFO, validation logic, parser.
 /-------------\
```

### 1.1 Kiểm thử đơn vị (Unit Tests)
- **Mục tiêu:** Kiểm tra các logic nghiệp vụ cô lập, không có kết nối DB hay thiết bị ngoại vi.
- **Phạm vi trọng tâm:** Thuật toán phân bổ của Rule Engine, tính toán UOM conversion factor, validation logic của Lot Expiry, và bộ lọc mã độc ZPL/TSPL.
- **Thư viện áp dụng:** xUnit cho C# / backend, Jest cho JavaScript / frontend.

### 1.2 Kiểm thử tích hợp (Integration Tests)
- **Mục tiêu:** Kiểm tra sự tương tác giữa code với database, cache, và phân quyền.
- **Phạm vi trọng tâm:** Giao dịch ghi sổ cái (`InventoryTransactions`) đảm bảo tính toàn vẹn transaction, kiểm tra trùng lặp `Idempotency-Key`, và check phân quyền RBAC.
- **Môi trường:** Chạy trên Docker Compose PostgreSQL cục bộ hoặc Testcontainers.

### 1.3 Kiểm thử E2E & UAT (E2E Tests)
- **Mục tiêu:** Kiểm tra luồng người dùng thật trên trình duyệt và thiết bị giả lập RF.
- **Phạm vi trọng tâm:** Nhận hàng PO -> Kiểm tra chất lượng -> Phân bổ SO -> Lấy hàng trên RF -> Đóng gói đọc cân cổng COM -> In nhãn ZPL -> Đóng thùng xuất kho.

---

## 2. Chiến lược dữ liệu kiểm thử (Test Data Strategy)

Để đảm bảo kết quả test nhất quán và không làm hỏng dữ liệu production, môi trường phát triển sử dụng bộ Seed Data chuẩn:

- **Demo Tenant:** `tenant_demo_01` (được cấu hình đầy đủ master data).
- **Master Data:**
  - 1 Warehouse chính (`wh_main_01`) có 3 Zones (`ZONE-RECEIVING`, `ZONE-DRY`, `ZONE-COOL`).
  - 50 Locations (phân bố từ kệ A01 đến A10, có cấu hình sức chứa).
  - 5 Items mẫu: sữa bột (quản lý theo Lot/Expiry), thiết bị điện tử (quản lý theo Serial), thùng nhựa (quản lý theo LPN), nước đóng chai (hàng thường), và cáp điện (quản lý theo mét).
- **User Roles:**
  - `user_receiver` (chỉ có quyền nhận hàng).
  - `user_qc` (quyền QC Hold/Release).
  - `user_picker` (quyền lấy hàng RF).
  - `user_packer` (quyền đóng gói, in nhãn).
  - `user_admin` (toàn quyền).

---

## 3. Các kịch bản kiểm thử phi chức năng đặc thù

### 3.1 Kiểm thử hiệu năng & Tải trọng (Load/Scale Scenarios)
1. **Quét tải đơn hàng lớn (Massive Allocation):** Giả lập đơn hàng SO chứa 200 items, tổng số lượng 5,000 dòng cần chạy thuật toán FEFO lock dòng tồn kho. Thời gian xử lý API `/allocate` phải dưới **1000ms**, không gây deadlock DB.
2. **Nhập kho hàng loạt (Import Burst):** Upload file Excel chứa 1,000 dòng Items mới. Đo tốc độ preview (dưới 3 giây) và commit (dưới 5 giây, bảo đảm rollback sạch nếu dòng 999 bị lỗi).
3. **Độ trễ quét mã RF (RF Scan Latency):** Giả lập 50 RF Scanner quét mã vạch đồng thời. Thời gian phản hồi API xác nhận vị trí phải đạt 95% dưới **300ms** trong mạng local.

### 3.2 Kiểm thử an toàn & Bảo mật (Security Scenarios)
1. **Cô lập Tenant (Tenant Isolation Audit):** Cố gắng truy vấn hoặc cập nhật bản ghi `InventoryBalances` của tenant `tenant_b` từ phiên đăng nhập (JWT) của `tenant_a`. Hệ thống phải lập tức trả lỗi `403 Forbidden` hoặc `404 Not Found`.
2. **Kiểm tra IDOR (Insecure Direct Object Reference):** Gửi request `POST /api/inbound/orders/{orderId}/receive` với `orderId` của tenant khác. Backend bắt buộc chặn tại middleware kiểm tra phạm vi sở hữu.
3. **Local Agent Spoofing:** Cố gắng mở kết nối WebSocket đến Local Agent từ trang web lạ (ví dụ: `https://attacker.com`). Agent bắt buộc reject kết nối ở bước handshake do sai Origin.

---

## 4. Yêu cầu bằng chứng kiểm thử (Evidence Requirements)

Với mỗi phase phát triển, Developer chính phải cung cấp đầy đủ các bằng chứng sau trước khi FOUNDER ký duyệt hoàn thành:

- **Bằng chứng Test Tự động:** Log chạy xUnit/Jest thành công (`100% pass` và code coverage tối thiểu 70% cho phần domain/business logic).
- **Bằng chứng UAT:** Video hoặc file gif ghi màn hình thao tác chạy thành công luồng nghiệp vụ trên môi trường local/staging.
- **Log giao dịch:** Ảnh chụp query SQL bảng `InventoryTransactions` chứng minh ghi nhận đúng ledger giao dịch sau mutation.

---

### 3.3 Kiểm thử contract tích hợp ERP (ERP Contract Testing)

**Vấn đề:** Khi SAP hoặc WMS legacy thay đổi payload format (thêm/xóa trường, đổi kiểu dữ liệu), integration test nội bộ vẫn pass nhưng môi trường production bị lỗi do contract drift.

**Giải pháp:** Áp dụng Schema-based Contract Validation thay vì full Pact (do tích hợp 1 chiều từ ERP → Nexustock):

1. **JSON Schema Contract File:** Lưu tại `tests/contracts/erp_inbound_order_schema.json` — định nghĩa cấu trúc payload hợp lệ nhận từ ERP (required fields, types, enum values, format).
2. **Contract Validation Test:** Mỗi Integration Test cho Phase 23 phải:
   - Load JSON Schema từ file contract
   - Validate payload mẫu (từ [erp_mock_payloads.md](file:///d:/1_Project/48_Nexustock/planning/enterprise/erp_mock_payloads.md)) bằng thư viện `JsonSchema.Net` (C#)
   - Assert mọi required field có đúng type và range
3. **CI Gate:** CI pipeline bắt buộc chạy contract test trước khi merge bất kỳ thay đổi nào vào Phase 23/24 code path.
4. **Version Control:** Mỗi khi SAP team thông báo thay đổi format, Developer phải cập nhật schema contract và tạo migration test case trước khi viết adapter code mới.

**Ownership & Trigger:**
- **Owner:** Dev chính (R — Responsible), FOUNDER review schema (A — Accountable)
- **Trigger — DoR requirement bắt buộc cho Phase 23:** Schema contract file `erp_inbound_order_schema.json` phải tồn tại và được FOUNDER approve TRƯỚC KHI bắt đầu code Phase 23. Không có schema = không được start Phase 23.
- **Maintenance SLA:** Khi SAP team gửi changelog format mới, Dev phải cập nhật schema trong vòng **1 ngày làm việc** và run contract test để confirm không break.

**Acceptance Criteria cho Phase 23 (Contract layer):**
- Contract schema file tồn tại và được commit vào repo
- Ít nhất 5 test case: happy path, thiếu required field, sai kiểu dữ liệu, duplicate idempotency key, payload quá lớn (> 1MB)
