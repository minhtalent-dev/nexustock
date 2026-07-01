# Support runbook (Sổ tay hướng dẫn xử lý sự cố vận hành WMS)

Tài liệu cung cấp quy trình chẩn đoán và khắc phục nhanh cho đội vận hành hệ thống (L1/L2 Support) khi xảy ra sự cố kỹ thuật hoặc nghiệp vụ tại nhà kho.

---

## 1. Hướng dẫn Lỗi kết nối thiết bị qua Local Agent (COM Scale / Zebra Printer)

### 1.1 Triệu chứng (Symptoms)
- Thủ kho đặt hàng lên cân nhưng Web UI không hiển thị cân nặng (nhảy liên tục `0.00` hoặc báo vòng tròn loading đỏ).
- Thủ kho bấm nút "In tem" nhưng máy in không chạy, trình duyệt hiện thông báo lỗi: `device.offline` hoặc `WebSocket connection failed`.

### 1.2 Quy trình chẩn đoán & Khắc phục nhanh (Steps to Resolve)
1. **Kiểm tra trạng thái Service cục bộ:**
   - Mở cửa sổ `Services.msc` trên máy tính trạm của thủ kho.
   - Tìm service `Nexustock Local Agent`. Nếu đang dừng (`Stopped`), chuột phải chọn `Start`.
2. **Kiểm tra cổng vật lý và cấu hình COM/USB:**
   - Mở `Device Manager` trên Windows để xem cân điện tử đang nhận cổng `COM` mấy (ví dụ: COM3 hoặc COM4).
   - Truy cập trang cấu hình của Local Agent tại địa chỉ local `http://localhost:9000/config` và kiểm tra xem cấu hình COM Port đã khớp với Device Manager chưa.
3. **Kiểm tra trạng thái kết nối WebSocket:**
   - Trên trình duyệt Web UI, bấm F12 mở DevTools, chọn tab Console.
   - Tìm kiếm log kết nối WebSocket đến `ws://127.0.0.1:9000`. Nếu lỗi CORS, kiểm tra cấu hình Origin Allowlist của Local Agent xem đã điền domain Web UI chính thức chưa.
4. **Quy trình nhập tay khẩn cấp (Manual Fallback Route):**
   - Nếu không sửa được phần cứng lập tức: Yêu cầu thủ kho chuyển sang chế độ "Nhập cân tay" (Manual Weight).
   - Thủ kho đọc số cân thực tế trên màn hình của cân và gõ vào ô nhập. Hệ thống bắt buộc yêu cầu chọn lý do: `REASON-DEVICE-DISCONNECT` (Thiết bị mất kết nối) và ghi nhận audit log để đối soát cuối ngày.

---

## 2. Hướng dẫn Lỗi nghẽn hàng đợi webhook / Outbox tích hợp ERP

### 2.1 Triệu chứng (Symptoms)
- Thủ kho đã đóng gói và xuất xe hàng, nhưng trên hệ thống ERP của đối tác vẫn báo trạng thái đơn hàng là "Đang chờ chuẩn bị".
- Bảng điều khiển admin WMS hiển thị cảnh báo số lượng tin nhắn trong Dead-Letter Queue (DLQ) tăng đột biến.

### 2.2 Quy trình chẩn đoán & Khắc phục nhanh (Steps to Resolve)
1. **Tìm Trace ID sự cố:**
   - Tìm mã đơn xuất kho bị chậm (ví dụ: `SO-2026-11223`).
   - Truy vấn bảng `IntegrationOutbox` để lấy danh sách tin nhắn liên quan đến đơn này. Ghi nhận mã `traceId` và cột `status`.
2. **Kiểm tra log lỗi kết nối:**
   - Đọc cột `errorMessage` hoặc log hệ thống theo `traceId` để xác định lỗi:
     - Lỗi `HTTP 401/403`: Lỗi xác thực Webhook Secret/API Key của ERP bị đổi.
     - Lỗi `HTTP 500/504`: Server ERP của đối tác bị sập hoặc quá tải.
     - Lỗi `HTTP 400`: Payload WMS gửi sang bị sai cấu hình mapping hoặc thiếu trường bắt buộc trên ERP.
3. **Xử lý và Replay tin nhắn:**
   - Nếu do lỗi phía ERP đối tác sập: Chờ đối tác khôi phục hệ thống, sau đó vào màn hình Admin Web UI, chọn danh sách tin DLQ và bấm "Replay Selected" để gửi lại hàng loạt.
   - Nếu do sai cấu hình mapping: Ops Admin cập nhật lại bảng mapping trong module Master Data của WMS, sau đó bấm Replay lại tin nhắn lỗi.

---

## 3. Hướng dẫn Khắc phục lệch số liệu tồn kho (Inventory Mismatch)

### 3.1 Triệu chứng (Symptoms)
- Hệ thống báo vị trí kệ `DRY-A-01` có `10` lon sữa Optimum, nhưng khi thủ kho đến lấy thì vị trí trống trơn.
- Web UI báo tồn kho khả dụng bằng `0` do có reservation ảo nhưng không tìm thấy Pick Task nào đang chạy để hủy.

### 3.2 Quy trình chẩn đoán & Khắc phục nhanh (Steps to Resolve)
1. **Truy vết lịch sử bằng Sổ cái (Ledger Audit):**
   - Chạy lệnh truy vấn SQL hoặc xem báo cáo lịch sử giao dịch tồn kho của Item tại vị trí `DRY-A-01`:
     ```sql
     SELECT * FROM "InventoryTransactions" 
     WHERE "itemId" = 'item_milk_dry' AND "locationId" = 'loc_dry_a01' 
     ORDER BY "createdAt" DESC;
     ```
   - Xác định dòng giao dịch cuối cùng làm thay đổi số dư và mã phiếu liên quan (`sourceId`, `traceId`).
2. **Xử lý Reservation bị treo (Ghost Reservations):**
   - Nếu phát hiện tồn khả dụng bị trừ do reservation bị treo: Kiểm tra bảng `AllocationReservations` xem có bản ghi nào quá hạn (`expiresAt < CURRENT_TIMESTAMP`) nhưng trạng thái vẫn ở `active`.
   - Chạy job quét và giải phóng reservation quá hạn (Clean-up Job).
3. **Quy trình Điều chỉnh kiểm kê đột xuất (Ad-hoc Stock Adjustment):**
   - Tạo phiếu kiểm kê đột xuất cho vị trí `DRY-A-01`.
   - Thủ kho quét vị trí và xác nhận số lượng thực tế là `0`.
   - Trưởng kho duyệt phiếu kiểm kê. Hệ thống tự động sinh giao dịch bù `COUNT_ADJUST` trừ `10` lon sữa trên hệ thống và ghi nhận lý do `REASON-STOCK-LOST` (Mất hàng vật lý).

---

## 4. Quy trình Cứu hộ khẩn cấp sập Database (Database Down Recovery)

### 4.1 Triệu chứng (Symptoms)
- Toàn bộ Web UI và RF không đăng nhập được, báo lỗi `500 Internal Server Error` hoặc `Database Connection Timeout`.

### 4.2 Quy trình cứu hộ (Escalation & Recovery Runbook)
1. **Xác định trạng thái Container/Service:**
   - SSH vào máy chủ cơ sở dữ liệu. Chạy lệnh: `docker ps` hoặc kiểm tra dịch vụ Systemd của PostgreSQL.
   - Nếu container DB bị dừng, chạy lệnh khởi động lại: `docker-compose restart db`.
2. **Khôi phục từ bản sao lưu gần nhất (Restore from Backup):**
   - Nếu ổ cứng hỏng hoặc dữ liệu bị lỗi vật lý không thể khôi phục:
     1. Dựng một instance Database trống mới.
     2. Lấy file backup mới nhất từ thư mục backup an toàn (`/var/backups/nexustock/*.sql.gz`).
     3. Giải nén và chạy lệnh import database:
        ```bash
        gunzip -c nexustock_backup_latest.sql.gz | psql -U postgres -d nexustock_main
        ```
     4. Chạy các migrations bổ sung (nếu phiên bản ứng dụng hiện tại mới hơn thời điểm backup).
3. **Đối soát dữ liệu tích hợp sau khôi phục:**
   - Do khôi phục từ thời điểm backup, dữ liệu trong khoảng RPO (ví dụ: 1 giờ gần nhất) sẽ bị mất trên WMS.
   - Ops Admin liên hệ quản trị ERP để chạy lệnh đồng bộ lại (Re-send/Replay) toàn bộ đơn PO/SO đã phát sinh trong 2 giờ gần nhất để WMS tự động ghi nhận lại qua cơ chế chống trùng lặp `Idempotency-Key`.
