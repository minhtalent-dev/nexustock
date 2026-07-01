# PHASE 6: LPN, SERIAL, RMA, WAVE PICKING & GENEALOGY

Phase này mở rộng năng lực WMS nâng cao sau khi core vận hành, RF/mobile và rule engine đã ổn định.

---

## 1. Mục tiêu

* Quản lý Pallet/LPN như đơn vị logistics chính.
* Truy vết Serial cho sản phẩm cần kiểm soát từng đơn vị.
* Xử lý hàng trả về RMA có QC phân loại.
* Gom đơn xuất bằng Wave Picking.
* Truy vết Material Genealogy theo cây Lot cha -> Lot con.

---

## 2. LPN

* Tạo LPN mới khi nhận hàng hoặc đóng pallet.
* Gán nhiều Lot vào một LPN.
* Di chuyển toàn bộ LPN bằng một lần quét.
* Kiểm soát trạng thái: `Active`, `Packed`, `Moved`, `Shipped`, `Closed`.
* Không cho tách/gộp LPN nếu vị trí đang khóa kiểm kê hoặc LPN đang xuất.

---

## 3. Serial number

* Cho phép cấu hình Item có cần tracking Serial hay không.
* Serial phải unique theo tenant và item.
* Mỗi Serial có vòng đời: nhận, QC, tồn kho, pick, pack, ship, return, scrap.
* Scan Serial bắt buộc ở picking/packing nếu item bật serial tracking.

---

## 4. RMA

* Tiếp nhận hàng trả theo return code hoặc shipment reference.
* QC phân loại: tái nhập, sửa chữa, cách ly, loại bỏ.
* Nếu tái nhập, phải tạo movement và cập nhật tồn rõ nguồn RMA.
* Nếu loại bỏ, không tăng tồn và phải ghi lý do.

---

## 5. Wave Picking

* Gom nhiều đơn xuất theo ngày, khách hàng, zone, item hoặc priority.
* Sinh pick list tối ưu theo vị trí và rule picking.
* Hỗ trợ pick theo Lot, LPN hoặc Serial.
* Ghi nhận short pick nếu thiếu hàng và tạo exception.

---

## 6. Material Genealogy

* Truy vết Lot cha -> Lot con khi chia nhỏ, phối trộn hoặc tái đóng gói.
* Cho phép khoanh vùng ảnh hưởng khi Lot lỗi QC.
* Hiển thị cây genealogy và activity timeline.
* Hỗ trợ khóa toàn bộ nhánh bị ảnh hưởng nếu có quyền QC/Manager.

---

## 7. API cần có

| API | Mục đích |
|---|---|
| `POST /api/lpn` | Tạo LPN |
| `POST /api/lpn/{id}/move` | Di chuyển LPN |
| `POST /api/serials/receive` | Nhận Serial |
| `POST /api/rma/receive` | Tiếp nhận RMA |
| `POST /api/waves` | Tạo wave picking |
| `GET /api/genealogy/lots/{lotNo}` | Tra cứu cây genealogy |

---

## 8. Tiêu chí hoàn tất

* LPN di chuyển hàng loạt đúng transaction.
* Serial không trùng và truy vết đủ vòng đời.
* RMA có quyết định QC và cập nhật tồn đúng.
* Wave Picking gom đơn đúng rule.
* Genealogy truy vết được Lot cha -> Lot con và hỗ trợ hold nhánh lỗi.
