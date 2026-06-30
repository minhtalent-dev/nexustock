# BÁO CÁO NGHIÊN CỨU & ĐỀ XUẤT PHÂN HỆ SMT BÁN DẪN (SEMICONDUCTOR MSL & IOT CONTROL)

Báo cáo này nghiên cứu sâu các tính năng vận hành kho nhạy cảm đặc thù trong các nhà máy sản xuất bán dẫn, điện tử công nghệ cao (SMT/Semiconductor) dựa trên các tiêu chuẩn quốc tế như **J-STD-020** và **J-STD-033D** (quản lý Moisture Sensitive Devices - MSD). Đây là các tính năng siêu chuyên biệt giúp Nexustock độc chiếm lợi thế cạnh tranh khi triển khai cho các tập đoàn công nghệ lớn.

---

## 📊 1. BẢNG ĐỐI CHIẾU TIÊU CHUẨN BÁN DẪN (SEMICONDUCTOR WMS BENCHMARK)

| Tính năng chuyên biệt | Tiêu chuẩn J-STD-033D | Hệ thống khác | Nexustock (Hiện tại) | Đánh giá & Khuyến nghị bổ sung |
|:---|:---:|:---:|:---:|:---|
| **MSL Class Classification**| ✅ Bắt buộc | ❌ Không | ❌ **Thiếu** | Cấu hình cấp độ nhạy ẩm của linh kiện (MSL 1, 2, 2a, 3, 4, 5, 5a, 6). |
| **Floor Life Tracking** | ✅ Bắt buộc | ❌ Không | ❌ **Thiếu** | Đếm ngược thời gian tiếp xúc môi trường thực tế của Lot ngay khi bóc túi chân không. |
| **Baking / Reset Control** | ✅ Bắt buộc | ❌ Không | ❌ **Thiếu** | Kiểm soát quá trình sấy phục hồi (Bake) và tạm dừng đếm Floor Life khi cất trong tủ sấy Dry Cabinet. |
| **IoT Sensor Monitoring** | ✅ Khuyên dùng | ✅ Có | ❌ **Thiếu** | Kết nối cảm biến nhiệt ẩm IoT thời gian thực để cảnh báo tủ sấy hoặc vùng kho bị vượt ngưỡng độ ẩm (ví dụ > 5% RH). |

---

## 🔍 2. CHI TIẾT CÁC TÍNH NĂNG ĐỀ XUẤT NÂNG CẤP (GAP ANALYSIS 5.0)

### A. Quản lý Cấp độ Nhạy ẩm (MSL - Moisture Sensitivity Level)
* **Vấn đề**: Các wafer hoặc chipset nhạy cảm nếu bị ẩm sẽ bị nổ/nứt khi qua lò hàn reflow (hiệu ứng popcorning). Hiện tại Nexustock chưa phân loại và kiểm soát các mức độ MSL.
* **Giải pháp đề xuất**:
  * Bổ sung trường `msl_level` (MSL 1, 2, 2a, 3, 4, 5, 5a, 6) vào cấu hình sản phẩm `ProductConfigs`.
  * Với mỗi cấp độ MSL, hệ thống tự động gán **Floor Life** tương ứng (Ví dụ: MSL 3 cho phép phơi ngoài không khí tối đa 168 giờ dưới điều kiện 30°C / 60% RH).

### B. Truy vết thời gian tiếp xúc môi trường (Floor Life Tracking)
* **Vấn đề**: Không có cơ chế kiểm soát xem một Lot linh kiện nhạy ẩm đã bị bóc bao bì và phơi ngoài môi trường bao lâu $\rightarrow$ Dẫn đến rủi ro đưa linh kiện đã hỏng độ ẩm vào sản xuất.
* **Giải pháp đề xuất**:
  * Xây dựng API và giao diện ghi nhận thao tác:
    * **Quét bóc túi (Unseal/Expose)**: Đánh dấu thời điểm bắt đầu phơi (`exposure_start_time`). Hệ thống bắt đầu đếm ngược Floor Life.
    * **Tạm dừng đếm (Pause)**: Khi Lot được cất lại vào tủ sấy khô (`Dry Cabinet` độ ẩm < 5% RH), hệ thống ghi nhận thời gian tiếp xúc tích lũy và tạm dừng bộ đếm.
  * **Khóa cứng xuất kho**: API xuất kho sẽ kiểm tra thời gian phơi tích lũy. Nếu vượt quá định mức Floor Life cho phép của MSL level, hệ thống chặn cứng không cho xuất và tự động chuyển Lot sang trạng thái **Hold chờ sấy**.

### C. Quản lý sấy phục hồi (Baking & Reset Life)
* **Vấn đề**: Linh kiện hết hạn phơi (Floor Life expired) bắt buộc phải sấy (Baking) để đuổi hơi ẩm ra ngoài. Việc quản lý sấy hiện nay làm thủ công trên giấy, dễ nhầm lẫn.
* **Giải pháp đề xuất**:
  * Thiết lập quy trình sấy:
    1. Công nhân quét Lot đưa vào tủ sấy nhiệt (`Baking Oven`). Hệ thống ghi nhận `baking_start_time`.
    2. API tự động tính toán thời gian sấy tối thiểu (Ví dụ: 24 giờ ở 125°C hoặc 9 ngày ở 40°C theo tiêu chuẩn J-STD-033D).
    3. Chặn không cho lấy Lot ra sớm. Khi sấy đủ thời gian, hệ thống tự động Reset Floor Life về ban đầu và mở khóa Lot sang trạng thái sẵn sàng sử dụng.

### D. Tích hợp cảm biến giám sát IoT (IoT Sensor Logs & Alarms)
* **Vấn đề**: Độ ẩm tủ Dry Cabinet bắt buộc phải dưới 5% RH để tạm dừng Floor Life. Nếu tủ bị hỏng hoặc quên đóng cửa làm độ ẩm tăng lên, linh kiện sẽ bị hỏng ngầm mà hệ thống không biết.
* **Giải pháp đề xuất**:
  * Nâng cấp **Local Agent**: Lắng nghe dữ liệu nhiệt độ/độ ẩm từ các cảm biến IoT lắp trong các tủ Dry Cabinet và tủ sấy qua giao thức MQTT/HTTP.
  * Backend API lưu log vào bảng `ZoneSensorLogs` định kỳ 5 phút/lần.
  * Nếu phát hiện tủ Dry Cabinet có độ ẩm vượt quá 5% RH trong hơn 30 phút liên tục:
    * Phát cảnh báo chuông còi tại kho thông qua Local Agent.
    * Tự động hủy lệnh tạm dừng và tiếp tục đếm ngược Floor Life của toàn bộ các Lot đang lưu bên trong tủ đó.

---

## 📐 3. SƠ ĐỒ CẤU TRÚC DATABASE BỔ SUNG (PROPOSED DB EXTENSIONS)

Để hỗ trợ các tính năng bán dẫn chuyên sâu này, cơ sở dữ liệu PostgreSQL cần được bổ sung các bảng sau:

```sql
-- 1. Bổ sung cấu hình MSL vào bảng ProductConfigs
ALTER TABLE product_configs ADD COLUMN msl_level VARCHAR(10) NULL; -- 'MSL-1', 'MSL-2', 'MSL-3', etc.
ALTER TABLE product_configs ADD COLUMN max_floor_life_hours INT NULL; -- Định mức số giờ phơi tối đa

-- 2. Bảng Lịch sử Tiếp xúc Độ ẩm & Sấy của Lot (Moisture Exposure Logs)
CREATE TABLE lot_moisture_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    lot_id UUID REFERENCES lots(id),
    action_type VARCHAR(50) NOT NULL, -- 'EXPOSE' (Bóc túi), 'PAUSE' (Cất tủ khô), 'BAKE_START', 'BAKE_END'
    operator_id UUID REFERENCES users(id),
    logged_at TIMESTAMP DEFAULT NOW(),
    accumulated_exposure_minutes INT DEFAULT 0, -- Số phút đã phơi lũy kế
    oven_temperature NUMERIC(5,2) NULL, -- Nhiệt độ tủ sấy (nếu là Bake)
    remarks TEXT
);

-- 3. Bảng Nhật ký Cảm biến IoT của Vùng Kho / Tủ Dry Cabinet
CREATE TABLE zone_sensor_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    zone_id UUID REFERENCES storage_zones(id),
    temperature NUMERIC(5,2) NOT NULL, -- Nhiệt độ (°C)
    humidity NUMERIC(5,2) NOT NULL, -- Độ ẩm (% RH)
    recorded_at TIMESTAMP DEFAULT NOW()
);
```

---

## ⚠️ ĐỀ XUẤT CỰC KỲ KHẨN CẤP: NEW CHAT

> [!CAUTION]
> **CẢNH BÁO: CHỈ SỐ CONTEXT ĐÃ VƯỢT QUÁ NGƯỠNG AN TOÀN (23/20 TURNS)**
>
> Phiên chat đã cực kỳ nặng. Để đảm bảo tính chính xác tuyệt đối của mã nguồn logic backend/frontend khi chúng ta chính thức code Phase 1 ở phiên tiếp theo, **Sếp hãy lưu tóm tắt và mở một cuộc trò chuyện mới (New Chat) ngay bây giờ**.
