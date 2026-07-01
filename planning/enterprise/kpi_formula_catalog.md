# KPI formula catalog (Danh mục công thức tính toán chỉ số hiệu năng WMS)

Tài liệu định nghĩa công thức toán học và cấu trúc dữ liệu để tính toán các chỉ số hiệu năng chính (KPI) phục vụ dashboard giám sát vận hành Nexustock WMS.

---

## 1. Hiệu suất Nhận hàng (Receiving Throughput)

- **Mục đích:** Đo lường khối lượng hàng hóa được tiếp nhận vào kho bởi mỗi nhân viên hoặc toàn kho trong một khoảng thời gian (ngày/ca).
- **Công thức:**
  $$\text{Receiving Throughput} = \sum (\text{receivedQty} \times \text{conversionFactor})$$
  *(Đơn vị quy đổi về Base UOM hoặc tính theo số lượng pallet/thùng nhập kho)*
- **Cơ sở dữ liệu:**
  - Bảng: `InventoryTransactions`
  - Điều kiện lọc: `transactionType = 'RECEIVE'` và `createdAt` nằm trong khoảng thời gian báo cáo.
- **Chiều phân tích (Dimensions):**
  - Theo nhân viên nhận: `createdBy`
  - Theo nhà cung cấp: `partnerId`
  - Theo kho: `warehouseId`

---

## 2. Độ chính xác lấy hàng (Pick Accuracy Rate)

- **Mục đích:** Đo lường tỷ lệ các nhiệm vụ lấy hàng được hoàn thành chính xác mà không gặp sự cố thiếu hàng hoặc sai mã.
- **Công thức:**
  $$\text{Pick Accuracy Rate} = \left( 1 - \frac{\text{Số nhiệm vụ Pick bị lỗi Short-Pick}}{\text{Tổng số nhiệm vụ Pick đã phát sinh}} \right) \times 100\%$$
- **Cơ sở dữ liệu:**
  - Bảng: `PickTasks` và `OperationalExceptions`
  - Số nhiệm vụ Short-Pick: Đếm các nhiệm vụ có `status = 'shortPicked'` hoặc liên kết với exception code là `SHORT_PICK`.
  - Tổng số nhiệm vụ: Đếm tất cả bản ghi `PickTasks` có trạng thái khác `cancelled`.

---

## 3. Tỷ lệ lệch kiểm kê (Cycle Count Variance Rate)

- **Mục đích:** Đo lường sai lệch giữa số lượng tồn kho trên hệ thống và số lượng thực tế kiểm đếm được tại vị trí.
- **Công thức:**
  $$\text{Variance Rate} = \frac{\sum |\text{systemQty} - \text{countedQty}|}{\sum \text{systemQty}} \times 100\%$$
  *(Tính trị tuyệt đối chênh lệch để tránh việc lệch dương bù lệch âm làm đẹp số liệu báo cáo)*
- **Cơ sở dữ liệu:**
  - Bảng: `CycleCountItems` (Bảng dòng chi tiết của phiếu kiểm kê).
  - Điều kiện lọc: Các phiếu kiểm kê đã hoàn tất và phê duyệt điều chỉnh.

---

## 4. Tỷ lệ Tích hợp Webhook thành công (Webhook Integration Success Rate)

- **Mục đích:** Giám sát tính ổn định của kết nối tích hợp truyền dữ liệu sang hệ thống bên thứ ba.
- **Công thức:**
  $$\text{Webhook Success Rate} = \frac{\text{Số Webhook gửi thành công (HTTP 2xx)}}{\text{Tổng số Webhook đã phát đi}} \times 100\%$$
- **Cơ sở dữ liệu:**
  - Bảng: `WebhookDeliveryLogs`
  - Thành công: Trạng thái `delivered` (hoặc response status code dạng `2xx`).
  - Tổng số: Gồm cả trạng thái `delivered`, `deadLetter` và đang trong quá trình `retryScheduled`.

---

## 5. Tỷ lệ In thành công và In lại (Print Success & Reprint Rate)

- **Mục đích:** Đánh giá độ ổn định của thiết bị máy in và phát hiện bất thường về dán sai tem nhãn.
- **Công thức:**
  - **Tỷ lệ lỗi in:**
    $$\text{Print Error Rate} = \frac{\text{Số lệnh in bị lỗi (timeout/offline)}}{\text{Tổng số lệnh in đã gửi}} \times 100\%$$
  - **Tỷ lệ in lại (Reprint Rate):**
    $$\text{Reprint Rate} = \frac{\text{Số lệnh in lại (Reprint)}}{\text{Tổng số nhãn in lần đầu thành công}} \times 100\%$$
- **Cơ sở dữ liệu:**
  - Bảng: `PrintJobs`
  - Lệnh in lỗi: `status = 'failed'`.
  - In lại: Các bản ghi có `isReprint = true` hoặc liên kết qua `originalPrintJobId`.

---

## 6. Tuổi thọ ngoại lệ vận hành trung bình (Average Exception Resolution Time)

- **Mục đích:** Đo lường tốc độ xử lý các sự cố phát sinh tại nhà kho (lệch vị trí, thiếu hàng, cân lỗi) của đội quản lý.
- **Công thức:**
  $$\text{Avg Resolution Time} = \frac{\sum (\text{resolvedAt} - \text{createdAt})}{\text{Tổng số ngoại lệ đã xử lý}}$$
- **Cơ sở dữ liệu:**
  - Bảng: `OperationalExceptions`
  - Điều kiện lọc: Trạng thái `resolved` hoặc `closed`.
  - Đơn vị đo: Phút hoặc Giờ.
- **Mục tiêu KPI vận hành (SLA):** 90% các ngoại lệ mức độ `critical` phải được giải quyết dưới **30 phút**.
