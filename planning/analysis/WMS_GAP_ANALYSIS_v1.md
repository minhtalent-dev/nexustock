# BÁO CÁO PHÂN TÍCH GAP & ĐỐI CHIẾU HỆ THỐNG QUẢN LÝ KHO (WMS GAP ANALYSIS)

Báo cáo này đối chiếu kế hoạch triển khai của **Nexustock** với các hệ thống quản lý kho (WMS) và quản lý tồn kho (Inventory) mã nguồn mở tiêu biểu trên GitHub như **ModernWMS**, **GreaterWMS** và **InvenTree**. Mục tiêu là đánh giá tính đầy đủ và phát hiện các tính năng còn thiếu để hoàn thiện hệ thống, sẵn sàng cho việc mở rộng flexible cho mọi nhà máy/công ty.

---

## 📊 1. BẢNG SO SÁNH TÍNH NĂNG (FEATURE BENCHMARK)

| Tính năng cốt lõi | ModernWMS | GreaterWMS | InvenTree | Nexustock (Hiện tại) | Đánh giá & Khuyến nghị cho Nexustock |
|:---|:---:|:---:|:---:|:---:|:---|
| **Quét Nhập/Xuất & Lot/Wafer** | ✅ Full | ✅ Full | ✅ Full | ✅ Đầy đủ | Kế thừa hoàn hảo từ nghiệp vụ cũ, tối ưu hóa qua WebSockets. |
| **Kiểm tra FIFO linh hoạt** | ❌ Hạn chế | ✅ Mạnh | ❌ Yếu | ✅ Đầy đủ | Hỗ trợ 3 cấp độ kiểm soát FIFO cấu hình động. |
| **Phần cứng Local Agent** | ❌ Không | ✅ Có (PDA) | ❌ Không | ✅ Đầy đủ | Tốt hơn các dự án free nhờ cơ chế in Raw ZPL/TSPL và WebSocket. |
| **Quản lý Hồ sơ Đối tác** | ✅ Có | ✅ Có | ✅ Có | ❌ **Thiếu** | Cần bổ sung để liên kết thông tin Invoice/Shipment với Vendor/Customer. |
| **Quy trình Kiểm kê định kỳ** | ✅ Có | ✅ Có | ✅ Có | ❌ **Thiếu** | Cần thiết lập quy trình kiểm kê quét mã và đối chiếu tự động. |
| **Cảnh báo Thông minh (Alerts)**| ✅ Có | ✅ Có | ✅ Có | ❌ **Thiếu** | Thiếu cảnh báo Lot sắp hết hạn (Expiration) và tồn tối thiểu/tối đa. |
| **Quản lý Định mức (BOM)** | ❌ Không | ❌ Không | ✅ Rất mạnh | ❌ **Thiếu** | Cần thiết nếu nhà máy có công đoạn lắp ráp/sản xuất thành phẩm. |
| **Tích hợp API ERP ngoài** | ❌ Không | ❌ Không | ✅ Có | ❌ **Thiếu** | Cần API Webhook nhận PO/Invoice tự động từ SAP/Odoo. |

---

## 🔍 2. CHI TIẾT CÁC PHÂN HỆ CẦN BỔ SUNG (GAP DETAILS)

### A. Phân hệ Quản lý Đối tác (Vendor & Customer Profiles)
* **Vấn đề**: Hiện tại Invoices chỉ lưu tên nhà cung cấp dạng text.
* **Giải pháp đề xuất**:
  * Thêm bảng `Partners` (Phân loại: `VENDOR` - Nhà cung cấp, `CUSTOMER` - Khách hàng).
  * Liên kết `Invoices.vendor_id` và `Shipments.customer_id` tới bảng `Partners`.
  * *Lợi ích*: Phân tích chất lượng giao hàng của từng Vendor (tỷ lệ Lot bị lỗi IQC) và quản lý lịch sử xuất kho chi tiết theo khách hàng.

### B. Phân hệ Kiểm kê Định kỳ (Cycle Counting & Stocktaking)
* **Vấn đề**: Chỉ có tính năng điều chỉnh tồn kho đơn lẻ (`StockAdjustment`), chưa có quy trình kiểm kê định kỳ tổng thể cho nhà kho.
* **Giải pháp đề xuất**:
  * Thiết kế bảng `Stocktakes` (Đợt kiểm kê) và `StocktakeItems` (Chi tiết quét kiểm kê).
  * **Luồng hoạt động**:
    1. Quản lý tạo đợt kiểm kê cho một khu vực kệ cụ thể.
    2. Công nhân cầm máy quét quét toàn bộ các Lot đang nằm trên kệ đó.
    3. Hệ thống tự động so sánh số lượng quét được với số tồn hệ thống.
    4. Trích xuất báo cáo chênh lệch và yêu cầu Quản lý phê duyệt để tự động cập nhật tồn kho.

### C. Hệ thống Cảnh báo Vận hành (Warehouse Alerts & Notifications)
* **Vấn đề**: Người vận hành kho phải tự tra cứu ngày hết hạn của Lot, dễ dẫn đến tình trạng Lot hết hạn nằm quên trong kho gây lãng phí.
* **Giải pháp đề xuất**:
  * Thiết kế bảng `Alerts` để ghi nhận các cảnh báo tự động:
    * **Expiration Alert**: Quét định kỳ mỗi ngày, tự động gửi cảnh báo cho các Lot có `expiration_date` còn dưới 30 ngày.
    * **Stock Level Alert**: Cảnh báo khi số lượng tồn của một mã vật tư trong kho xuống dưới mức tối thiểu (`min_stock`) hoặc vượt mức tối đa (`max_stock`).
  * Hiển thị danh sách cảnh báo nổi bật trên Dashboard của Next.js Frontend.

---

## 📐 3. SƠ ĐỒ CẤU TRÚC DATABASE BỔ SUNG (PROPOSED DB EXTENSIONS)

Để hỗ trợ các phân hệ thiếu hụt trên, cấu trúc cơ sở dữ liệu PostgreSQL cần được bổ sung các bảng sau:

```sql
-- 1. Quản lý Đối tác
CREATE TABLE partners (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES tenants(id),
    name VARCHAR(150) NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    type VARCHAR(20) NOT NULL, -- 'VENDOR', 'CUSTOMER'
    address TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

-- 2. Quản lý Đợt Kiểm kê
CREATE TABLE stocktakes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES tenants(id),
    title VARCHAR(100) NOT NULL,
    warehouse_id UUID REFERENCES warehouses(id),
    status VARCHAR(20) DEFAULT 'OPEN', -- 'OPEN', 'COMPLETED', 'APPROVED'
    created_by UUID REFERENCES users(id),
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE stocktake_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    stocktake_id UUID REFERENCES stocktakes(id),
    location_id UUID REFERENCES storage_locations(id),
    lot_id UUID REFERENCES lots(id),
    system_qty NUMERIC(12,3) NOT NULL,
    scanned_qty NUMERIC(12,3) NOT NULL,
    scanned_at TIMESTAMP DEFAULT NOW()
);

-- 3. Cấu hình Cảnh báo Tồn kho (Bổ sung vào bảng Products/ProductConfigs)
ALTER TABLE product_configs ADD COLUMN min_stock NUMERIC(12,3) DEFAULT 0;
ALTER TABLE product_configs ADD COLUMN max_stock NUMERIC(12,3) DEFAULT 999999;
```

---

## 🎯 4. HÀNH ĐỘNG TIẾP THEO (RECOMMENDED ACTIONS)

* **Bước 1**: Nhận phản hồi của sếp về các tính năng thiếu hụt này.
* **Bước 2**: Tiến hành cập nhật trực tiếp cấu trúc Database (Phase 2), API xử lý (Phase 3) và giao diện UI (Phase 4) vào kế hoạch chính **`IMPLEMENTATION_PLAN.md`** để đảm bảo tính nhất quán cao nhất.
