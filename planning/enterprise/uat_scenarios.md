# UAT scenarios (Kịch bản kiểm thử nghiệm thu người dùng)

Tài liệu hướng dẫn kịch bản UAT thực tế theo vai trò người dùng trong kho hàng.

---

## Vai trò người dùng (Warehouse Roles)

1. **Thủ kho nhận hàng (Receiver):** Chịu trách nhiệm kiểm đếm, scan mã vạch và ghi nhận hàng nhập.
2. **Nhân viên kiểm soát chất lượng (QC Inspector):** Kiểm tra Lot hàng, thực hiện khóa (Hold) hoặc mở khóa (Release) hàng.
3. **Nhân viên lấy hàng (Picker):** Sử dụng RF/mobile để đi lấy hàng theo vị trí gợi ý.
4. **Nhân viên đóng gói (Packer):** Kiểm tra hàng tại bàn đóng gói, cân trọng lượng carton và in tem nhãn dán thùng.
5. **Nhân viên kiểm kê (Inventory Controller):** Thực hiện đếm hàng định kỳ, điều chỉnh chênh lệch tồn kho.
6. **Quản trị hệ thống/Vận hành (Ops Admin):** Cấu hình rule, giám sát tích hợp ERP, webhook, xử lý sự cố thiết bị.

---

## Kịch bản UAT chi tiết

### UAT-01: Nhận hàng thực tế từ đơn mua hàng PO (Vai trò: Receiver)

- **Mục tiêu:** Kiểm tra luồng nhận hàng đầy đủ, tạo Lot và cập nhật tồn kho chính xác.
- **Điều kiện đầu vào (Preconditions):**
  - Đã có đơn PO `PO-2026-0001` từ ERP gửi sang trạng thái `open`.
  - Item `ITEM-001` (Sữa bột) đã được khai báo, quản lý theo Lot, hạn dùng 365 ngày.
  - Vị trí khu vực nhận hàng `LOC-RECEIVING-01` trống.
- **Các bước thực hiện:**
  1. Đăng nhập Web UI/RF bằng tài khoản `receiver_01`.
  2. Mở màn hình "Nhận hàng" (Inbound Receiving), tìm đơn `PO-2026-0001`.
  3. Chọn Item `ITEM-001`. Quét barcode trên sản phẩm.
  4. Nhập mã Lot mới: `LOT-MILK-001`. Nhập ngày sản xuất và ngày hết hạn.
  5. Nhập số lượng thực nhận: `100` lon. Bấm "Xác nhận nhận hàng".
- **Kết quả mong đợi:**
  - Phiếu nhập chuyển sang trạng thái `receiving` hoặc `completed`.
  - Một dòng giao dịch `RECEIVE` được thêm vào bảng `InventoryTransactions`.
  - Tồn kho tại `LOC-RECEIVING-01` tăng `100` lon với Lot `LOT-MILK-001`, trạng thái QC mặc định là `qcPending`.
- **Bằng chứng kiểm thử (Evidence Required):**
  - Chụp ảnh màn hình xác nhận nhận hàng thành công trên RF.
  - Kết quả truy vấn SQL bảng `InventoryBalances` và `InventoryTransactions` chứng minh tồn kho tăng và có dòng ledger tương ứng.

---

### UAT-02: Kiểm tra chất lượng và Khóa/Mở khóa tồn kho (Vai trò: QC Inspector)

- **Mục tiêu:** Khóa Lot hàng nghi ngờ lỗi và kiểm tra xem Rule Engine có chặn không cho phân bổ xuất kho.
- **Điều kiện đầu vào (Preconditions):**
  - Lot `LOT-MILK-001` có `100` sản phẩm đang ở trạng thái `qcPending`.
- **Các bước thực hiện:**
  1. Đăng nhập Web UI bằng tài khoản `qc_01`.
  2. Mở màn hình "Quản lý lô hàng" (Lot Management), tìm `LOT-MILK-001`.
  3. Bấm nút "Khóa lô hàng" (Hold Lot). Chọn lý do: `REASON-QC-SUSPECT` (Nghi ngờ móp vỏ). Bấm xác nhận.
  4. Sang tài khoản Nhân viên xuất kho, cố gắng tạo đơn xuất và bấm phân bổ (`allocate`) cho sản phẩm `ITEM-001`.
- **Kết quả mong đợi:**
  - Trạng thái QC của lô chuyển thành `hold`.
  - Bảng `AuditLogs` ghi nhận tác vụ Hold Lot kèm người thực hiện và lý do.
  - Thao tác phân bổ xuất hàng trả lỗi: `inventory.insufficientAvailableQty` (do hàng bị QC Hold không được tính vào tồn kho khả dụng).
- **Bằng chứng kiểm thử (Evidence Required):**
  - Chụp màn hình trạng thái lô hàng hiển thị màu đỏ (Hold).
  - API response lỗi 400 kèm code `inventory.insufficientAvailableQty`.

---

### UAT-03: Lấy hàng và Đóng gói Carton kết hợp cân điện tử (Vai trò: Picker & Packer)

- **Mục tiêu:** Lấy hàng theo vị trí gợi ý, cân trọng lượng qua Local Agent và đóng carton.
- **Điều kiện đầu vào (Preconditions):**
  - Đơn xuất `SO-2026-0001` đã được phân bổ thành công cho Lot `LOT-MILK-001` tại vị trí `LOC-STORAGE-A1`.
  - Local Agent trên máy tính bàn đóng gói đang chạy và đã ghép cặp với WebSocket trạm `STATION-PACK-01`. Cân điện tử kết nối qua cổng COM3 hoạt động bình thường.
- **Các bước thực hiện:**
  1. (Picker) Đăng nhập RF, nhận nhiệm vụ lấy hàng `PICK-TASK-001`. RF hiển thị đi đến vị trí `LOC-STORAGE-A1`.
  2. (Picker) Quét mã vị trí `LOC-STORAGE-A1`, quét mã sản phẩm `ITEM-001`, nhập số lượng lấy `5` lon. Bấm "Hoàn tất".
  3. (Packer) Nhận rổ hàng 5 lon tại bàn đóng gói, mở màn hình "Đóng gói" trên Web UI.
  4. (Packer) Quét mã đơn hàng để mở phiên đóng gói. Đặt carton chứa hàng lên cân điện tử.
  5. (Packer) Web UI tự động hiển thị trọng lượng ổn định đọc từ cân qua WebSocket Local Agent (ví dụ: `2.50 kg`). Bấm "Đóng thùng".
- **Kết quả mong đợi:**
  - Nhiệm vụ PickTask chuyển sang `completed`.
  - Trọng lượng hiển thị trên màn hình Web khớp 100% với số cân thực tế mà không cần nhập tay.
  - Thùng hàng được cấp mã Carton `CTN-2026-0001` và trạng thái đơn xuất chuyển sang `packed`.
- **Bằng chứng kiểm thử (Evidence Required):**
  - Video quay cảnh đặt hàng lên cân và số cân nhảy tự động trên màn hình Web UI (WSS connection log hiển thị nhận payload weight).

---

### UAT-04: Xử lý sự cố mất kết nối máy in tem nhãn (Vai trò: Ops Admin)

- **Mục tiêu:** Kiểm tra khả năng tự phục hồi của hàng đợi in khi máy in gặp sự cố và thao tác in lại (Reprint).
- **Điều kiện đầu vào (Preconditions):**
  - Máy in Zebra ZPL đang tắt nguồn hoặc rút cáp USB.
- **Các bước thực hiện:**
  1. Đăng nhập tài khoản `packer_01`, bấm in tem nhãn pallet cho Lot hàng vừa nhận.
  2. Quan sát hệ thống báo lỗi in ấn sau 10 giây timeout.
  3. Admin đăng nhập Web UI, vào "Giám sát in ấn" (Print Monitoring) để kiểm tra trạng thái lệnh in.
  4. Cắm lại cáp máy in và bật nguồn.
  5. Admin chọn lệnh in lỗi trong danh sách, bấm "Gửi lại lệnh in" (Replay/Retry Job).
- **Kết quả mong đợi:**
  - Lần in đầu tiên ghi nhận trạng thái `failed` trong bảng `PrintJobs` kèm log lỗi `device.offline`.
  - Sau khi bật máy in và bấm gửi lại, máy in Zebra in ra tem nhãn thành công.
  - Trạng thái PrintJob chuyển thành `printed`.
- **Bằng chứng kiểm thử (Evidence Required):**
  - Log của Local Agent ghi nhận exception connection timeout và log ghi nhận gửi thành công khi retry.
