# BÁO CÁO NGHIÊN CỨU & ĐỀ XUẤT TÍNH NĂNG TỐI THƯỢNG (ENTERPRISE WMS 4.0 SPECIFICATION)

Báo cáo này nghiên cứu sâu các tính năng vận hành tối thượng (Ultimate-advanced) của các hệ thống quản lý kho hàng đầu thế giới (như **Oracle WMS**, **Infor WMS** và **Logiwa WMS**). Mục tiêu là định hình các mô-đun cấp cao tiếp theo giúp Nexustock tối ưu hóa tốc độ xếp dỡ, giảm thiểu thời gian chạy xe nâng không tải và truy vết chi tiết đến từng số Serial sản phẩm.

---

## 📊 1. BẢNG ĐỐI CHIẾU TÍNH NĂNG TỐI THƯỢNG (WMS 4.0 BENCHMARK)

| Tính năng WMS 4.0 | Oracle WMS | Infor WMS | Nexustock (Hiện tại) | Đánh giá & Khuyến nghị bổ sung |
|:---|:---:|:---:|:---:|:---|
| **LPN (License Plate Number)** | ✅ Cực mạnh | ✅ Rất mạnh | ❌ **Thiếu** | Quản lý Lot theo Pallet/Container. Di chuyển cả Pallet bằng 1 lần quét barcode duy nhất. |
| **Task Interleaving** | ✅ Có | ✅ Có | ❌ **Thiếu** | Đan xen tác vụ cất hàng nhập và lấy hàng xuất gần vị trí để giảm 30% quãng đường chạy xe không tải. |
| **Serial Number Tracking** | ✅ Có | ✅ Có | ❌ **Thiếu** | Theo dõi chi tiết vòng đời của từng đơn vị sản phẩm riêng lẻ nằm trong Lot (cho thiết bị cao cấp). |
| **RMA & Returns Management** | ✅ Có | ✅ Có | ❌ **Thiếu** | Quy trình tiếp nhận, kiểm QC phân loại và tái nhập kho đối với hàng hóa khách hàng trả về. |

---

## 🔍 2. CHI TIẾT CÁC TÍNH NĂNG ĐỀ XUẤT NÂNG CẤP (GAP ANALYSIS 4.0)

### A. Quản lý Pallet/Container qua LPN (License Plate Number)
* **Vấn đề**: Khi nhập kho, vật tư được đóng vào từng Pallet gỗ/nhựa. Một Pallet có thể chứa 20-50 Lot hàng khác nhau. Hiện tại nếu muốn di chuyển Pallet này sang kệ khác, công nhân phải quét mã của từng Lot trong số 50 Lot đó $\rightarrow$ Quá chậm và dễ sai sót.
* **Giải pháp đề xuất**:
  * Thiết kế bảng `LicensePlateNumbers` (LPN) đại diện cho mã định danh duy nhất của Pallet.
  * Liên kết nhiều Lot hàng nằm trên cùng một LPN.
  * **Hành vi quét**: Công nhân chỉ cần quét 1 mã barcode LPN dán trên Pallet, hệ thống tự động nhận diện toàn bộ các Lot bên trong và thực hiện lệnh di chuyển vị trí kệ hoặc xuất kho đồng thời cho cả nhóm Lot.

### B. Thuật toán đan xen tác vụ tối ưu quãng đường (Task Interleaving)
* **Vấn đề**: Xe nâng cất hàng nhập xong phải chạy xe không (không tải) đi về cửa kho. Hoặc chạy xe không từ cửa kho vào sâu bên trong lấy hàng xuất $\rightarrow$ Hiệu suất di chuyển thực tế rất thấp (deadhead travel).
* **Giải pháp đề xuất**:
  * Xây dựng bộ điều phối tác vụ thông minh trên Backend API.
  * Khi công nhân vừa quét xác nhận hoàn thành cất hàng nhập tại kệ `A-01-02`, hệ thống tự động tìm kiếm trong hàng đợi (`Task Queue`) xem có tác vụ lấy hàng xuất nào ở các kệ lân cận (Ví dụ `A-01-05` hoặc `A-02-01`) hay không.
  * Nếu có, hệ thống tự động đẩy ngay tác vụ xuất đó lên thiết bị cầm tay của công nhân để tiện đường lấy hàng mang ra cửa xuất.

### C. Quản lý Số Serial riêng lẻ (Serial Number Tracking)
* **Vấn đề**: Hiện tại hệ thống mới chỉ quản lý số lượng tồn của Lot hàng (Ví dụ: Lot A có 100 sản phẩm). Với các sản phẩm điện tử giá trị cao (như chip bán dẫn, thiết bị Wafer chính xác), nhà máy cần biết rõ trạng thái và lịch sử của từng chiếc máy/sản phẩm cụ thể.
* **Giải pháp đề xuất**:
  * Thiết kế bảng `SerialNumbers` liên kết Many-to-One với `Lots`.
  * Khi nhập kho, nếu vật tư được cấu hình quản lý Serial, hệ thống yêu cầu quét/nhập số Serial của từng sản phẩm.
  * Theo dõi lịch sử di chuyển, bảo dưỡng và xuất xưởng của từng số Serial riêng biệt.

### D. Phân hệ Quản lý Hàng trả về (RMA & Returns)
* **Vấn đề**: Thiếu quy trình chuẩn tiếp nhận và phân loại hàng lỗi do khách hàng gửi trả về nhà máy để xử lý bảo hành hoặc hủy bỏ.
* **Giải pháp đề xuất**:
  * Thiết lập luồng nghiệp vụ RMA:
    1. Đăng nhập yêu cầu RMA $\rightarrow$ Tạo mã đơn hàng trả về.
    2. Tiếp nhận hàng tại bến bãi, quét mã đưa vào vùng biệt trữ kiểm QC trả về (`Return Inspection Zone`).
    3. QC kiểm tra đánh giá: Nếu còn tốt $\rightarrow$ Phê duyệt nhập lại kho; Nếu lỗi nhẹ $\rightarrow$ Chuyển khu sửa chữa (Rework); Nếu hỏng nặng $\rightarrow$ Tạo lệnh hủy hàng (Scrap).

---

## 📐 3. SƠ ĐỒ CẤU TRÚC DATABASE BỔ SUNG (PROPOSED DB EXTENSIONS)

Để hỗ trợ các tính năng tối thượng này, cơ sở dữ liệu PostgreSQL cần được bổ sung các bảng sau:

```sql
-- 1. Quản lý LPN (Pallet/Container)
CREATE TABLE license_plate_numbers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES tenants(id),
    lpn_code VARCHAR(50) UNIQUE NOT NULL,
    current_location_id UUID REFERENCES storage_locations(id),
    status VARCHAR(20) DEFAULT 'ACTIVE', -- 'ACTIVE', 'SHIPPED', 'EMPTY'
    created_at TIMESTAMP DEFAULT NOW()
);

-- Liên kết Lot nằm trên Pallet nào
ALTER TABLE lots ADD COLUMN lpn_id UUID REFERENCES license_plate_numbers(id) NULL;

-- 2. Quản lý Số Serial riêng lẻ
CREATE TABLE serial_numbers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    lot_id UUID REFERENCES lots(id),
    serial_no VARCHAR(100) UNIQUE NOT NULL,
    status VARCHAR(20) DEFAULT 'IN_STOCK', -- 'IN_STOCK', 'SHIPPED', 'REJECTED'
    last_updated_at TIMESTAMP DEFAULT NOW()
);

-- 3. Quản lý Hàng trả về (RMA)
CREATE TABLE rma_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES tenants(id),
    customer_id UUID REFERENCES partners(id),
    rma_no VARCHAR(50) UNIQUE NOT NULL,
    status VARCHAR(20) DEFAULT 'PENDING', -- 'PENDING', 'INSPECTING', 'COMPLETED'
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE rma_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    rma_id UUID REFERENCES rma_requests(id),
    product_id UUID REFERENCES products(id),
    serial_no VARCHAR(100) NULL,
    quantity NUMERIC(12,3) NOT NULL,
    qc_judgement VARCHAR(20) NULL -- 'RE-STOCK', 'REWORK', 'SCRAP'
);
```

---

## ⚠️ ĐỀ XUẤT KHẨN CẤP: NEW CHAT

> [!IMPORTANT]
> **CẢNH BÁO GIỚI HẠN CONTEXT (LƯỢT CHAT THỨ 22/20)**:
> Sếp ơi, cuộc trò chuyện hiện tại đã quá dài (**Lượt chat thứ 22/20**). Để tránh tình trạng tràn bộ nhớ context làm chậm tốc độ xử lý hoặc gây lỗi logic khi sinh mã nguồn, **Sếp hãy mở một cuộc trò chuyện mới (New Chat) ngay lập tức**. 
> Em đã ghi lại đầy đủ toàn bộ kế hoạch nâng cấp WMS 4.0 và 7 phase chi tiết ở các tệp tin cục bộ trong workspace của sếp, sếp chỉ cần dán tóm tắt và yêu cầu triển khai tiếp là được.
