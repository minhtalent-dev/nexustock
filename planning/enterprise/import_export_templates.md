# Import and export templates (Cấu trúc tệp tin Import/Export mẫu)

Tài liệu định nghĩa chi tiết cấu trúc cột, kiểu dữ liệu và ràng buộc validation cho các tệp Excel/CSV dùng để nhập/xuất dữ liệu trong hệ thống Nexustock WMS.

---

## 1. Mẫu nhập danh mục Hàng hóa (Item Master Import)

- **Định dạng tệp tin:** Excel (`.xlsx`) hoặc CSV (UTF-8, dấu phân cách `,`).
- **Tên tệp khuyến nghị:** `item_import_template.csv`

### Cấu trúc cột dữ liệu:

| Tên cột (Header) | Kiểu dữ liệu | Bắt buộc | Ràng buộc Validation (Business Rules) | Ví dụ |
|---|---|---|---|---|
| `itemCode` | String | **Có** | - Dài 3 - 30 ký tự, không chứa dấu cách hoặc ký tự đặc biệt.<br>- Phải độc nhất (unique) trong hệ thống của tenant. | `MILK-DRY-900` |
| `itemName` | String | **Có** | - Dài 5 - 150 ký tự. | `Sữa bột Optimum 900g` |
| `trackingPolicy` | Enum String | **Có** | Chỉ chấp nhận các giá trị: `Normal` (không theo lô), `Lot` (theo lô), `Lot_Expiry` (lô + hạn dùng), `Serial` (theo số serial từng cái). | `Lot_Expiry` |
| `shelfLifeDays` | Integer | **Không**| - Bắt buộc lớn hơn 0 nếu `trackingPolicy` là `Lot_Expiry`. | `730` |
| `baseUomCode` | String | **Có** | - Phải tồn tại trong danh mục Đơn vị tính (UOM) của tenant. | `LON` |
| `status` | String | **Không**| Chỉ nhận `active` hoặc `inactive`. Mặc định nếu bỏ trống: `active`. | `active` |

---

## 2. Mẫu nhập danh mục Vị trí kho (Location Import)

- **Mục đích:** Khai báo nhanh sơ đồ vị trí (sức chứa kệ Pallet) khi cấu hình kho mới.
- **Tên tệp khuyến nghị:** `location_import_template.csv`

### Cấu trúc cột dữ liệu:

| Tên cột (Header) | Kiểu dữ liệu | Bắt buộc | Ràng buộc Validation (Business Rules) | Ví dụ |
|---|---|---|---|---|
| `warehouseCode` | String | **Có** | Phải là mã kho đang tồn tại và hoạt động. | `wh_hn_01` |
| `zoneCode` | String | **Có** | Phải là mã khu vực tồn tại trong kho chỉ định. | `DRY_ZONE` |
| `locationCode` | String | **Có** | - Ràng buộc unique toàn kho.<br>- Định dạng chuẩn khuyến nghị: `Khu-Dãy-Kệ-Tầng` (ví dụ: `DRY-A-01-02`). | `DRY-A-01-02` |
| `locationType` | Enum String | **Có** | Chỉ nhận: `storage` (lưu trữ), `qc` (kiểm tra), `staging` (tạm), `shipping` (xuất), `quarantine` (cách ly). | `storage` |
| `capacityPallet`| Integer | **Có** | Sức chứa tối đa tính theo Pallet. Phải > 0. | `2` |
| `status` | String | **Không**| Nhận `active` hoặc `inactive`. Mặc định: `active`. | `active` |

---

## 3. Mẫu xuất báo cáo Tồn kho chi tiết (Inventory Balance Export)

- **Mục đích:** Xuất dữ liệu đối soát tồn kho thực tế gửi sang ERP hoặc in báo cáo cuối ngày.
- **Định dạng đầu ra:** Excel (`.xlsx`) hoặc CSV.

### Danh sách cột xuất ra:

| Tên cột hiển thị | Khóa JSON (camelCase) | Ý nghĩa nghiệp vụ | Ghi chú định dạng |
|---|---|---|---|
| **Mã Kho** | `warehouseCode` | Mã kho vật lý | `wh_hn_01` |
| **Khu Vực** | `zoneCode` | Mã khu vực | `DRY_ZONE` |
| **Vị Trí** | `locationCode` | Vị trí cụ thể trên kệ | `DRY-A-01-01` |
| **Mã Hàng** | `itemCode` | Mã sản phẩm | `MILK-DRY-900` |
| **Tên Hàng** | `itemName` | Tên sản phẩm | `Sữa bột Optimum 900g` |
| **Mã Lô (Lot)** | `lotNo` | Lô sản xuất của hàng | Trống nếu không quản lý theo lô |
| **Số Lượng** | `qty` | Số lượng tồn vật lý thực tế | Định dạng số thập phân `#,##0.00` |
| **Khả Dụng** | `availableQty` | Số lượng khả dụng (trừ đi hàng đã giữ xuất) | Định dạng số thập phân |
| **Đơn Vị Tính**| `uomCode` | Đơn vị tính | `LON` |
| **Trạng Thái QC**| `inventoryStatus` | Trạng thái chất lượng tồn kho | `released` (Hợp lệ) / `hold` (Bị khóa) |
| **Mã Pallet/LPN**| `lpnId` | Mã thùng/Pallet chứa hàng | Trống nếu để hàng lẻ |
| **Ngày Hết Hạn**| `expiryDate` | Hạn sử dụng của lô hàng | Định dạng ngày `YYYY-MM-DD` |

---

## 4. Quy trình xử lý lỗi khi Import dữ liệu (Import Validation Protocol)

1. **Bước 1: Preview & Kiểm tra cấu trúc (Structure Check)**
   - Upload file lên API preview. Hệ thống kiểm tra tên cột, định dạng tệp tin. Nếu sai cấu trúc, trả lỗi ngay lập tức mà không phân tích tiếp.
2. **Bước 2: Validate dòng dữ liệu (Row-level Validation)**
   - Quét từng dòng dữ liệu trong file: Kiểm tra kiểu dữ liệu, bắt buộc nhập, độ dài chuỗi, kiểm tra logic chéo (ví dụ: ngày sản xuất < ngày hết hạn).
   - Kiểm tra khóa ngoại (Foreign Key): Mã Item, Warehouse, Zone, UOM có tồn tại trong database không.
3. **Bước 3: Tổng hợp báo cáo Preview (Import Preview Response)**
   - API trả về danh sách các lỗi theo cấu trúc: `{ row: 12, column: "trackingPolicy", errorCode: "validation.invalidEnum", message: "Chỉ chấp nhận các giá trị Normal, Lot, Lot_Expiry, Serial." }`.
   - Giao diện Web hiển thị bảng preview: Dòng đỏ là dòng lỗi kèm tooltip giải thích, dòng xanh là dòng hợp lệ.
4. **Bước 4: Commit an toàn (Atomic Commit)**
   - Người dùng bấm "Xác nhận Import". API chỉ commit dữ liệu vào Database khi **100% dòng trong file hợp lệ**. Nếu có bất kỳ dòng nào lỗi, hệ thống chặn không cho ghi DB để tránh tình trạng import rác nửa vời.
