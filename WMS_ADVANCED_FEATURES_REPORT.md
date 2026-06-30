# BÁO CÁO NGHIÊN CỨU & ĐỀ XUẤT TÍNH NĂNG NÂNG CAO CHO NEXUSTOCK (ADVANCED WMS FEATURES)

Báo cáo này đối chiếu kế hoạch triển khai của **Nexustock** với các hệ thống quản lý kho cấp cao doanh nghiệp (tiêu biểu như **SAP EWM - Extended Warehouse Management**, **GreaterWMS** và **Odoo WMS**). Mục tiêu là tìm ra các khoảng trống tính năng nâng cao để biến Nexustock thành giải pháp kho thông minh toàn diện, tối ưu hóa năng suất vận hành thực tế.

---

## 📊 1. ĐỐI CHIẾU CÁC TÍNH NĂNG NÂNG CAO (ENTERPRISE WMS COMPARISON)

| Tính năng nâng cao | SAP EWM | Odoo WMS | Nexustock (Hiện tại) | Đánh giá & Khuyến nghị bổ sung |
|:---|:---:|:---:|:---:|:---|
| **Zoning (Vùng kho bảo quản)** | ✅ Có | ✅ Có | ❌ **Thiếu** | Cần phân chia kho thành các vùng bảo quản đặc thù (Kho thường, kho mát, vùng biệt trữ QC). |
| **Putaway Strategy & Slotting** | ✅ Rất mạnh | ✅ Có | ❌ **Thiếu** | Tự động đề xuất vị trí kệ tối ưu khi nhập kho dựa trên đặc tính hàng hóa (nặng/nhẹ, đi nhanh/chậm). |
| **Cross-Docking (Chuyển tiếp)** | ✅ Có | ✅ Có | ❌ **Thiếu** | Cho phép chuyển thẳng hàng từ cửa nhập sang cửa xuất, bỏ qua cất kệ nếu có đơn hàng xuất khẩn cấp. |
| **Automated Scale Integration**| ✅ Có (RFID) | ❌ Không | ❌ **Thiếu** | Kết nối trực tiếp cân điện tử qua Local Agent để tự động lấy trọng lượng thực tế khi đóng gói. |
| **Universal Barcoding** | ✅ Có | ✅ Có | ❌ **Thiếu** | In mã QR cho Vị trí kệ, User, Đợt kiểm kê để quét nhanh xác nhận hành động tại hiện trường. |

---

## 🔍 2. CHI TIẾT CÁC TÍNH NĂNG ĐỀ XUẤT NÂNG CẤP (GAP ANALYSIS 2.0)

### A. Phân hoạch Vùng Kho (Storage Zones) & Quy tắc bảo quản
* **Vấn đề**: Các vị trí kệ (`StorageLocations`) hiện tại xếp chung một nhóm, chưa phân biệt điều kiện bảo quản vật tư (Ví dụ: Wafer nhạy cảm cần lưu kho mát, hóa chất keo dán cần kho đông lạnh).
* **Giải pháp đề xuất**:
  * Thêm bảng `StorageZones` (Vùng lưu trữ: Kho mát, Kho thường, Khu biệt trữ QC, Khu xuất hàng).
  * Liên kết `StorageLocations` thuộc về một `StorageZone`.
  * Ràng buộc logic trên API Backend: Ngăn chặn xếp hàng hóa chất vào vùng kho thường, hoặc tự động định tuyến Lot mới nhập chưa qua QC vào vùng biệt trữ QC.

### B. Quy tắc cất hàng tối ưu (Putaway & Slotting Strategy)
* **Vấn đề**: Công nhân tự tìm vị trí kệ trống để cất hàng, dễ dẫn đến việc xếp hàng nặng ở tầng cao (gây nguy hiểm) hoặc hàng đi nhanh nằm ở góc sâu (tốn thời gian lấy).
* **Giải pháp đề xuất**:
  * Bổ sung các chỉ số vào cấu hình sản phẩm (`ProductConfigs`):
    * `weight_class`: Nặng / Trung bình / Nhẹ.
    * `rotation_speed`: Nhanh (Fast-moving) / Chậm (Slow-moving).
  * Khi quét nhập kho, API Backend tự động phân tích và đề xuất 3 vị trí kệ trống tối ưu:
    * Hàng nặng $\rightarrow$ Đề xuất các kệ ở tầng 1 (dưới cùng).
    * Hàng đi nhanh $\rightarrow$ Đề xuất các kệ gần cửa xuất hàng để tối ưu quãng đường di chuyển.

### C. Luồng chuyển tiếp trực tiếp (Cross-Docking Flow)
* **Vấn đề**: Hàng hóa khi nhập về luôn phải qua quy trình: Nhập $\rightarrow$ Cất kệ $\rightarrow$ Chờ xuất $\rightarrow$ Lấy hàng xuất. Gây lãng phí thời gian nếu đang có đơn xuất khẩn cấp chờ sẵn.
* **Giải pháp đề xuất**:
  * Khi thực hiện quét nhập kho (`PartInput`), API Backend kiểm tra xem mã vật tư này có nằm trong danh sách các đơn xuất kho (`Shipments`) đang ở trạng thái chờ xuất hay không.
  * Nếu khớp, hệ thống hiển thị thông báo đề xuất chuyển thẳng Lot này sang khu vực xuất hàng (Cross-Docking Zone) để đóng gói xuất xưởng ngay lập tức, bỏ qua bước cất lên kệ.

### D. Tích hợp Cân điện tử tự động (Scale Serial Integration)
* **Vấn đề**: Khi đóng gói xuất khẩu (Phase 6), công nhân phải đọc số cân thủ công và gõ tay vào trường trọng lượng (`scanned_weight`), dễ xảy ra sai sót nhập liệu hoặc gian lận số liệu cân.
* **Giải pháp đề xuất**:
  * Nâng cấp **Local Agent** (Phase 5): Lắng nghe thêm cổng Serial kết nối với Cân điện tử tại bàn đóng gói.
  * Khi công nhân đặt thùng hàng lên cân, Local Agent tự động đọc trị số cân thực tế và truyền thời gian thực qua WebSocket lên Web UI.
  * Ô nhập trọng lượng trên Web SPA sẽ tự động điền số cân và khóa sửa đổi (Read-only) để đảm bảo tính trung thực 100% của dữ liệu vận đơn.

### E. Mã vạch thực thể đa năng (Universal Entity Barcoding)
* **Vấn đề**: Máy quét cầm tay mới chỉ dùng để quét mã Lot. Các tác vụ chọn vị trí kệ, đăng nhập user vẫn phải gõ thủ công trên bàn phím.
* **Giải pháp đề xuất**:
  * Cho phép in nhãn mã vạch/QR cho mọi thực thể:
    * **Nhãn kệ hàng**: Dán mã QR tại từng ô kệ để công nhân quét xác nhận "Đã cất hàng đúng kệ" (Double check vị trí).
    * **Thẻ nhân viên**: In mã QR định danh tài khoản để quét đăng nhập nhanh hoặc xác nhận người vận hành (Operator ID).
    * **Nhãn đợt kiểm kê/Vận đơn**: Quét mã để tự động tải thông tin đợt kiểm kê hoặc vận đơn tương ứng lên màn hình.

---

## 📐 3. THIẾT KẾ CẤU TRÚC DATABASE BỔ SUNG (PROPOSED DB EXTENSIONS)

Để hỗ trợ các tính năng nâng cao trên, cơ sở dữ liệu PostgreSQL cần được bổ sung các bảng sau:

```sql
-- 1. Quản lý Vùng Kho
CREATE TABLE storage_zones (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    warehouse_id UUID REFERENCES warehouses(id),
    name VARCHAR(100) NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    temperature_limit NUMERIC(5,2) NULL, -- Giới hạn nhiệt độ bảo quản
    description TEXT
);

-- Bổ sung liên kết vùng kho vào StorageLocations
ALTER TABLE storage_locations ADD COLUMN zone_id UUID REFERENCES storage_zones(id);

-- 2. Cấu hình đặc tính sản phẩm phục vụ Slotting (Bổ sung vào ProductConfigs)
ALTER TABLE product_configs ADD COLUMN weight_class VARCHAR(20) DEFAULT 'MEDIUM'; -- 'HEAVY', 'MEDIUM', 'LIGHT'
ALTER TABLE product_configs ADD COLUMN rotation_speed VARCHAR(20) DEFAULT 'SLOW'; -- 'FAST', 'MEDIUM', 'SLOW'
```

---

## 🎯 4. HÀNH ĐỘNG TIẾP THEO (RECOMMENDED ACTIONS)

* Báo cáo này đã làm rõ các tính năng nâng cao chuẩn doanh nghiệp.
* Chờ chỉ thị của sếp xem có cần tích hợp các hạng mục này (Zoning, Slotting, Cân điện tử tự động, Mã QR kệ hàng) vào kế hoạch triển khai chính hay không.
