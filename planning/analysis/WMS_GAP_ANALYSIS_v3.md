# BÁO CÁO NGHIÊN CỨU & ĐỀ XUẤT TÍNH NĂNG SIÊU NÂNG CAO (ENTERPRISE WMS 3.0 SPECIFICATION)

Báo cáo này nghiên cứu sâu các tính năng vận hành siêu nâng cao (Ultra-advanced) của các hệ thống WMS hàng đầu thế giới (như **SAP EWM**, **Manhattan Active WMS** và **InvenTree**). Mục tiêu là định hình các mô-đun cấp cao tiếp theo giúp Nexustock tối ưu hóa hoàn toàn năng lực xử lý, truy vết chất lượng 100% (Traceability) và nâng cao hiệu suất lao động tại các nhà máy quy mô lớn.

---

## 📊 1. BẢNG ĐỐI CHIẾU TÍNH NĂNG SIÊU NÂNG CAO (ENTERPRISE 3.0 COMPARISON)

| Tính năng siêu nâng cao | SAP EWM | Manhattan WMS | Nexustock (Hiện tại) | Đánh giá & Khuyến nghị bổ sung |
|:---|:---:|:---:|:---:|:---|
| **Wave & Batch Picking** | ✅ Rất mạnh | ✅ Rất mạnh | ❌ **Thiếu** | Gom nhiều đơn xuất lẻ thành các đợt (Wave) lấy hàng để tối ưu hóa quãng đường di chuyển của nhân viên. |
| **Material Genealogy Tree** | ❌ Yếu | ❌ Yếu | ❌ **Thiếu** | Vẽ sơ đồ cây gia phả Lot cha $\rightarrow$ Lot con (Kowake Tree) để truy vết nguồn gốc vật tư khi xảy ra lỗi chất lượng. |
| **Labor & Task Tracking** | ✅ Có | ✅ Rất mạnh | ❌ **Thiếu** | Ghi nhận thời gian bắt đầu/kết thúc và định mức hiệu suất của từng công nhân để tính toán KPI tự động. |
| **Dock & Yard Scheduling** | ✅ Có | ✅ Có | ❌ **Thiếu** | Lập lịch hẹn cho xe container/xe tải cập các cửa kho (Dock Door) để bốc dỡ hàng, tránh ùn tắc bãi đỗ. |

---

## 🔍 2. CHI TIẾT CÁC TÍNH NĂNG ĐỀ XUẤT NÂNG CẤP (GAP ANALYSIS 3.0)

### A. Quy hoạch Đợt gom hàng xuất (Wave Picking & Batch Picking)
* **Vấn đề**: Hiện tại công nhân kho xuất hàng theo từng vận đơn lẻ (`Shipments`). Nếu có 20 đơn xuất lẻ cho 20 khách hàng khác nhau nhưng cùng lấy một loại linh kiện, công nhân sẽ phải đi vào kho lấy hàng 20 lần riêng biệt $\rightarrow$ Tốn 90% thời gian di chuyển vô ích.
* **Giải pháp đề xuất**:
  * Thiết kế bảng `PickingWaves` (Đợt gom hàng) để nhóm nhiều đơn xuất kho có cùng khu vực lấy hàng hoặc cùng mã linh kiện.
  * **Luồng hoạt động**:
    1. Hệ thống tự động gom các đơn hàng xuất trong khung giờ $\rightarrow$ Tạo một `PickingWave`.
    2. API tạo danh sách gom tổng hợp (Pick List) $\rightarrow$ Công nhân chỉ cần đi 1 lần lấy toàn bộ số lượng linh kiện của 20 đơn.
    3. Mang hàng ra khu vực phân loại (Sorting Zone) để quét chia nhỏ đóng gói cho từng khách hàng.

### B. Cây gia phả truy vết nguồn gốc vật tư (Material Genealogy & Kowake Tree)
* **Vấn đề**: Khi khách hàng phản hồi một lô sản phẩm bị lỗi linh kiện, nhà máy cực kỳ khó khăn để tìm ngược lại xem lô linh kiện đó được cắt ra từ Lot cha nào, nhập khẩu ngày nào, thuộc Invoice nào.
* **Giải pháp đề xuất**:
  * Xây dựng API và giao diện Web UI hiển thị **Sơ đồ cây gia phả (Genealogy Tree)** trực quan sử dụng Mermaid hoặc React Flow.
  * Khi nhập mã Lot con (`inner_lot_no`) bất kỳ, hệ thống vẽ ngược lên Lot cha gốc (`parent_lot_id`), hiển thị lịch sử: PO đặt hàng $\rightarrow$ Vendor giao hàng $\rightarrow$ Kết quả duyệt IQC $\rightarrow$ Lịch sử di chuyển kệ $\rightarrow$ Các Lot con khác cùng dòng họ được chia tách.
  * *Lợi ích*: Khoanh vùng và khóa (Hold) khẩn cấp toàn bộ các Lot con liên quan khi phát hiện một Lot bị lỗi chất lượng.

### C. Quản lý Hiệu suất Lao động (Labor & Task Management)
* **Vấn đề**: Quản lý kho không biết được công nhân nào làm việc năng suất, công nhân nào làm việc chậm hoặc phân bổ công việc không đều.
* **Giải pháp đề xuất**:
  * Thiết kế bảng `LaborTasks` ghi nhận chi tiết thời gian:
    * `task_type`: Nhập kho, QC, Chia tách, Di chuyển kệ, Gom hàng.
    * `start_time` (khi công nhân mở màn hình/quét barcode nhận việc).
    * `end_time` (khi click hoàn thành).
  * Backend tự động tính toán thời gian thực hiện trung bình của từng loại tác vụ và trích xuất bảng xếp hạng năng suất (KPI Dashboard).

### D. Lập lịch Cửa xuất nhập kho (Dock & Yard Scheduling)
* **Vấn đề**: Các xe container của nhà cung cấp đến giao hàng hoặc xe tải của khách hàng đến lấy hàng cùng một lúc, gây ùn tắc nghiêm trọng tại cửa kho (Dock doors).
* **Giải pháp đề xuất**:
  * Thiết kế bảng `DockDoors` đại diện cho các cửa kho và `DockAppointments` (Lịch hẹn cập cửa).
  * Cho phép đối tác đăng ký trước khung giờ xe đến giao/nhận hàng.
  * Hệ thống tự động sắp xếp cửa kho trống và thông báo cho tài xế, tránh ùn tắc bến bãi.

---

## 📐 3. SƠ ĐỒ CẤU TRÚC DATABASE BỔ SUNG (PROPOSED DB EXTENSIONS)

Để hỗ trợ các phân hệ siêu nâng cao này, cơ sở dữ liệu PostgreSQL cần được bổ sung các bảng sau:

```sql
-- 1. Quản lý Đợt gom hàng xuất (Wave Picking)
CREATE TABLE picking_waves (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES tenants(id),
    wave_no VARCHAR(50) UNIQUE NOT NULL,
    status VARCHAR(20) DEFAULT 'OPEN', -- 'OPEN', 'PICKING', 'SORTING', 'COMPLETED'
    created_at TIMESTAMP DEFAULT NOW()
);

-- Liên kết các Shipment Items vào Đợt gom hàng
ALTER TABLE shipment_items ADD COLUMN wave_id UUID REFERENCES picking_waves(id);

-- 2. Quản lý Hiệu suất Lao động
CREATE TABLE labor_tasks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES tenants(id),
    user_id UUID REFERENCES users(id),
    task_type VARCHAR(50) NOT NULL, -- 'INPUT', 'QC', 'SPLIT', 'PICK', 'MOVE'
    reference_id UUID NULL, -- ID của Lot, Stocktake hoặc Shipment tương ứng
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP NULL,
    status VARCHAR(20) DEFAULT 'IN_PROGRESS'
);

-- 3. Quản lý Lịch hẹn Cửa kho (Dock Door)
CREATE TABLE dock_doors (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    warehouse_id UUID REFERENCES warehouses(id),
    door_no VARCHAR(20) NOT NULL,
    status VARCHAR(20) DEFAULT 'AVAILABLE' -- 'AVAILABLE', 'OCCUPIED', 'MAINTENANCE'
);

CREATE TABLE dock_appointments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    dock_id UUID REFERENCES dock_doors(id),
    partner_id UUID REFERENCES partners(id),
    vehicle_plate VARCHAR(30) NOT NULL,
    scheduled_time TIMESTAMP NOT NULL,
    duration_minutes INT DEFAULT 60,
    status VARCHAR(20) DEFAULT 'SCHEDULED' -- 'SCHEDULED', 'ACTIVE', 'COMPLETED', 'MISSED'
);
```

---

## ⚠️ KHUYẾN NGHỊ AN TOÀN HỆ THỐNG & ĐỀ XUẤT NEW CHAT

> [!WARNING]
> **CẢNH BÁO GIỚI HẠN NGỮ CẢNH (CONTEXT LIMIT REACHED)**: Cuộc hội thoại hiện tại đã đạt đến giới hạn an toàn (**Lượt chat thứ 21/20**). Để tránh tình trạng AI bị quá tải bộ nhớ, dẫn đến việc phản hồi chậm hoặc xảy ra sai sót không đáng có trong các bước thực thi code tiếp theo, **Sếp hãy mở một cuộc trò chuyện mới ngay lập tức (New Chat)** và dán lại tóm tắt này để chúng ta tiếp tục triển khai dự án một cách mượt mà nhất.
