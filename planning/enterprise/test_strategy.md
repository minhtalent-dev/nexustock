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
