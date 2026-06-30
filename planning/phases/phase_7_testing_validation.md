# PHASE 7: KẾ HOẠCH KIỂM THỬ TOÀN DIỆN & XÁC THỰC HỆ THỐNG (TESTING & VALIDATION)

Phase này thiết lập toàn bộ quy trình, kịch bản và công cụ để tiến hành kiểm thử tự động (Unit Test, Integration Test, End-to-End Test) và kiểm thử thủ công cho hệ thống **Nexustock**, đảm bảo phần mềm hoạt động ổn định, không phát sinh lỗi hồi quy trước khi đưa vào sản xuất.

---

## 🧪 1. KIẾN TRÚC KIỂM THỬ (TESTING ARCHITECTURE)

Hệ thống kiểm thử được chia làm 3 tầng độc lập:
```
+-------------------------------------------------------+
|  Tầng 3: End-to-End (E2E) UI Testing (Playwright)     |
|  - Kiểm tra toàn bộ luồng nghiệp vụ trên trình duyệt  |
+---------------------------+---------------------------+
                            |
+---------------------------v---------------------------+
|  Tầng 2: API Integration Testing (ASP.NET Core Test)  |
|  - Kiểm tra tính toàn vẹn DB Transaction, JWT Auth    |
+---------------------------+---------------------------+
                            |
+---------------------------v---------------------------+
|  Tầng 1: Unit Testing (xUnit / Jest)                  |
|  - Kiểm tra logic FIFO, định dạng Lot Regex, Mocking   |
+-------------------------------------------------------+
```

---

## 🛠️ 2. CHI TIẾT CÁC TẦNG KIỂM THỬ (TEST SUITES)

### A. TẦNG 1: KIỂM THỬ ĐƠN VỊ (UNIT TESTING)

#### 1. Backend Unit Tests (`xUnit` + `FluentAssertions`)
Tập trung kiểm thử các thuật toán logic độc lập, không phụ thuộc vào kết nối cơ sở dữ liệu thật (Sử dụng Mock DB hoặc InMemory DB).
* **Mục tiêu chính**:
  * Thuật toán kiểm tra FIFO (kiểm tra so sánh ngày sản xuất các lô hàng).
  * Định dạng sinh mã Lot (Lot No Pattern) theo cấu hình của từng Tenant.
  * Regex validation cho mã Lot của từng nhà sản xuất.
* **Mẫu mã nguồn Test**:
  ```csharp
  public class FifoServiceTests
  {
      [Fact]
      public async Task CheckFifo_ShouldFail_WhenOlderLotExists()
      {
          // 1. Arrange: Thiết lập dữ liệu giả lập (Mock)
          var options = new DbContextOptionsBuilder<NexustockDbContext>()
              .UseInMemoryDatabase(databaseName: "Test_Fifo_Db")
              .Options;

          using var context = new NexustockDbContext(options);
          var tenantId = Guid.NewGuid();
          
          context.TenantConfigs.Add(new TenantConfig { TenantId = tenantId, FifoPolicyLevel = 2 });
          context.Lots.Add(new Lot { ProductCode = "PART001", LotNo = "LOT-OLD", CurrentQty = 100, ManufactureDate = DateTime.Today.AddDays(-10) });
          context.Lots.Add(new Lot { ProductCode = "PART001", LotNo = "LOT-NEW", CurrentQty = 100, ManufactureDate = DateTime.Today });
          await context.SaveChangesAsync();

          var service = new FifoService(context);

          // 2. Act: Thực thi hàm kiểm tra
          var result = await service.CheckFifoAsync("PART001", "LOT-NEW", tenantId);

          // 3. Assert: Kiểm tra kết quả mong muốn
          result.IsValid.Should().BeFalse();
          result.ErrorMessage.Should().Contain("Vi phạm quy tắc FIFO");
      }
  }
  ```

#### 2. Frontend Unit Tests (`Jest` + `React Testing Library`)
* **Mục tiêu chính**:
  * Kiểm tra component `HasPermission` ẩn/hiện chính xác phần tử UI theo Claims.
  * Kiểm tra logic chuyển đổi đơn vị đo lường khi nhập liệu (Ví dụ: Kg -> Gram).

---

### B. TẦNG 2: KIỂM THỬ TÍCH HỢP API (INTEGRATION TESTING)

Sử dụng thư viện `Microsoft.AspNetCore.Mvc.Testing` để chạy Web Server giả lập trong bộ nhớ, gửi request HTTP thực tế kiểm tra toàn bộ luồng xử lý của Controller $\rightarrow$ Service $\rightarrow$ PostgreSQL Database thật.

* **Kịch bản Test 1: Kiểm thử Xác thực và Phân quyền API**
  * Gửi request `POST /api/part-input/accept` không đính kèm JWT Token $\rightarrow$ Kết quả mong muốn: `401 Unauthorized`.
  * Gửi request đính kèm Token có vai trò `OPERATOR` nhưng không có quyền `material.accept` $\rightarrow$ Kết quả mong muốn: `403 Forbidden`.
  * Gửi request với đầy đủ quyền $\rightarrow$ Kết quả mong muốn: `200 OK`.
* **Kịch bản Test 2: Kiểm thử Concurrency Lock (Khóa đồng thời chống race condition)**
  * Chạy đồng thời 5 luồng (Thread) gửi yêu cầu xuất kho cùng 1 Lot hàng có số lượng tồn là 10. Mỗi luồng yêu cầu xuất 3 sản phẩm.
  * Kết quả mong muốn: Chỉ có 3 luồng đầu tiên xuất kho thành công (Số lượng tồn giảm về 1). 2 luồng cuối cùng bị chặn lại và nhận mã lỗi `409 Conflict` (Lỗi tranh chấp tồn kho) hoặc `400 Bad Request` (Không đủ số lượng tồn). Giao dịch rollback an toàn, không có hiện tượng âm kho.
* **Kịch bản Test 3: Kiểm thử luồng tích hợp Phê duyệt Kiểm kê (Stocktake Integration Flow)**
  * Thực hiện phê duyệt đợt kiểm kê chênh lệch (`POST /api/stocktake/approve/{id}`).
  * Kết quả mong muốn: Toàn bộ quá trình chạy thành công tạo phiếu `StockAdjustment` và cân bằng số tồn `Inventories` hoặc rollback hoàn toàn nếu xảy ra sự cố.
* **Kịch bản Test 4: Kiểm thử thuật toán Slotting đề xuất cất hàng tối ưu**
  * Thiết lập dữ liệu Mock cho các vị trí kệ ở tầng cao (`A-01-05`) và tầng thấp (`A-01-01`).
  * Thực hiện request nhập kho Lot hàng nặng (`weight_class = 'HEAVY'`).
  * Kết quả mong muốn: Danh sách đề xuất bắt buộc phải đề xuất các vị trí ở tầng thấp (`A-01-01`), không đề xuất tầng cao.
* **Kịch bản Test 5: Kiểm thử gom hàng xuất Wave Picking**
  * Thiết lập 3 Shipment Items lấy cùng một loại vật tư ở cùng 1 vị trí kệ.
  * Gửi yêu cầu lấy Pick List của Wave.
  * Kết quả mong muốn: API trả về đúng 1 bản ghi gom tổng cộng số lượng của cả 3 đơn hàng, không trả về 3 dòng rời rạc.
* **Kịch bản Test 6: Kiểm thử Truy vết Gia phả Lot (Material Genealogy)**
  * Tạo dữ liệu Lot gốc, sau đó chạy API Kowake chia nhỏ thành 3 Lot con.
  * Gọi API `GET /api/lot-traceability/{lotNo}/genealogy` cho một Lot con bất kỳ.
  * Kết quả mong muốn: Cấu trúc JSON trả về chứa đầy đủ thông tin mối quan hệ phân cấp cây có gốc là Lot gốc.
* **Kịch bản Test 7: Kiểm thử Ghi nhận thời gian Năng suất lao động (Labor Tracking)**
  * Gọi API `POST /api/labor/start-task` và sau 3 giây gọi API `POST /api/labor/end-task/{id}`.
  * Kết quả mong muốn: Bản ghi Task trong Database được lưu với thời gian hoàn thành hợp lệ và trạng thái `COMPLETED`.
* **Kịch bản Test 8: Kiểm thử đóng gói và di chuyển Pallet LPN**
  * Tạo LPN mới, gán 10 Lot hàng vào LPN đó.
  * Gửi request di chuyển LPN sang vị trí kệ mới `B-05-05`.
  * Kết quả mong muốn: API thực thi Transaction cập nhật thành công vị trí của cả LPN và vị trí của cả 10 Lot hàng bên trong trong bảng `Inventories`.
* **Kịch bản Test 9: Kiểm thử thuật toán đan xen tác vụ (Task Interleaving)**
  * Nhân viên hoàn thành cất hàng nhập tại kệ A-01. Có tác vụ pick hàng xuất ở kệ A-02 đang PENDING.
  * Gọi API check task tiếp theo.
  * Kết quả mong muốn: Hệ thống tự động gán và chuyển trạng thái task pick xuất tại kệ A-02 sang IN_PROGRESS cho chính nhân viên này.
* **Kịch bản Test 10: Chặn xuất nhập tại vị trí bị khóa kiểm kê (Location Lock)**
  * Chuyển trạng thái `is_locked = true` cho kệ `A-01-01`.
  * Gọi API nhập hàng hoặc API di chuyển Pallet LPN tới vị trí `A-01-01`.
  * Kết quả mong muốn: API trả về lỗi `400 Bad Request` hoặc `Conflict`, các bản ghi tồn kho không thay đổi.
* **Kịch bản Test 11: Phê duyệt cân thô nhập tay ghi log**
  * Gửi request đóng gói sản phẩm qua API `POST /api/shipment/pack-item` với tham số `IsManualWeight = true` và `ManualWeightReason = 'Cân đứt cáp RS232'`.
  * Kết quả mong muốn: API lưu thành công bản ghi `ShipmentItem` với cờ `is_manual_weight = true` và lưu đúng lý do vào cột `manual_weight_reason`.

---

### C. TẦNG 3: KIỂM THỬ TOÀN DIỆN GIAO DIỆN (END-TO-END TESTING)

Sử dụng công cụ **Playwright** để viết các kịch bản kiểm thử tự động giả lập hành vi của người dùng bấm chuột trên trình duyệt Next.js SPA thực tế kết nối với API Backend.

* **Kịch bản E2E 1: Luồng Nhập kho, QC & Đề xuất Slotting**
  1. Mở trình duyệt, đăng nhập bằng tài khoản Operator.
  2. Vào màn hình "Tiếp nhận vật tư".
  3. Quét Lot hàng nặng $\rightarrow$ Xác nhận 3 vị trí đề xuất hiển thị ở tầng thấp.
  4. Xác nhận nhập kho $\rightarrow$ Bản ghi Lot mới được tạo với trạng thái QC chờ duyệt.
* **Kịch bản E2E 2: Kiểm tra chặn xuất vi phạm FIFO**
  1. Đăng nhập bằng tài khoản Operator.
  2. Vào màn hình "Đóng gói & Xuất hàng".
  3. Quét một Lot mới sản xuất trong khi Lot cũ cùng loại vẫn còn tồn $\rightarrow$ Popup cảnh báo vi phạm FIFO hiển thị, nút "Đóng gói" bị khóa.
  4. Nhập mã phê duyệt của Manager $\rightarrow$ Khóa được mở để tiếp tục.
* **Kịch bản E2E 3: Luồng kiểm kê, đóng băng kệ và phê duyệt chênh lệch**
  1. Đăng nhập bằng tài khoản Manager, vào màn hình kiểm kê, click "Đóng băng" để khóa vị trí kệ `A-01-01`.
  2. Đăng nhập tài khoản Operator, thử di chuyển LPN tới `A-01-01` $\rightarrow$ Xác nhận hệ thống hiển thị thông báo lỗi bị chặn do đang kiểm kê.
  3. Vào màn hình "Kiểm kê", quét vị trí kệ và quét Lot hàng thực tế $\rightarrow$ Hệ thống tự động so khớp hiển thị hàng lệch màu đỏ.
  4. Đăng nhập lại bằng tài khoản Manager, click nút "Phê duyệt & Tự động cân bằng kho", đồng thời hệ thống tự động mở khóa vị trí kệ `A-01-01` (`is_locked = false`).
  5. Xác nhận số lượng tồn trong bảng `Inventories` đã được cập nhật bằng số thực tế.
* **Kịch bản E2E 4: Tự động hóa cân điện tử và dự phòng cân tay khi đóng gói**
  1. Vào màn hình đóng gói vận đơn xuất kho.
  2. Trình duyệt thiết lập kết nối WebSocket với Local Agent.
  3. Mô phỏng mất kết nối WebSocket (cân điện tử tắt nguồn) $\rightarrow$ Trình duyệt hiển thị cảnh báo đỏ "Mất kết nối cân".
  4. Operator click "Nhập cân tay" $\rightarrow$ Hiển thị ô nhập số cân và ô bắt buộc nhập lý do.
  5. Nhập số cân `15.80` và lý do "Cân đứt cáp", xác nhận đóng gói $\rightarrow$ Gửi thành công request kèm log và mở khóa lưu database.
* **Kịch bản E2E 5: Luồng gom hàng xuất Wave Picking**
  1. Đăng nhập tài khoản Manager, vào màn hình Wave Picking, chọn 5 đơn hàng xuất kho của ngày, click "Tạo đợt gom".
  2. Đăng nhập tài khoản Operator, mở màn hình đợt gom tương ứng, xem Pick List tổng hợp và thực hiện quét mã lấy hàng hàng loạt.
* **Kịch bản E2E 6: Tra cứu gia phả và khoanh vùng sự cố**
  1. Vào màn hình Gia phả, nhập mã Lot bị lỗi chất lượng.
  2. Xác nhận Sơ đồ phả hệ hiển thị đúng Lot gốc và các Lot con cùng nhánh.
  3. Đăng nhập tài khoản QC Inspector, click "Khóa khẩn cấp toàn bộ nhánh gia phả" $\rightarrow$ Xác nhận tất cả các Lot con cùng nhánh trong kho tự động chuyển sang trạng thái `hold_status = true`.
* **Kịch bản E2E 7: Đóng gói và quét LPN Pallet**
  1. Vào màn hình LPN Pallet, tạo mã Pallet mới, quét 5 mã Lot để đóng Pallet.
  2. Vào màn hình Chuyển kho, quét mã Pallet LPN, quét vị trí kệ mới $\rightarrow$ Hệ thống cập nhật thành công, không cần quét lại 5 mã Lot.
* **Kịch bản E2E 8: Nhận hàng trả lại RMA & QC phân loại**
  1. Vào màn hình RMA, quét nhận mã hàng trả lại từ khách hàng.
  2. QC chọn phán quyết "Scrap" (Hủy bỏ) $\rightarrow$ Xác nhận số tồn không tăng và tạo bản ghi lịch sử hủy hàng thành công.

---

## 📋 3. BẢNG TIÊU CHÍ NGHIỆM THU ĐẦU RA (ACCEPTANCE CRITERIA)

Hệ thống Nexustock chỉ được phép phát hành lên production khi vượt qua toàn bộ các tiêu chí kiểm thử sau:

| STT | Loại kiểm tra | Tiêu chuẩn đạt | Phượng pháp xác nhận |
|---|---|---|---|
| 1 | Unit Test Coverage | Đạt tối thiểu **80%** độ bao phủ dòng code của phần core nghiệp vụ | Chạy công cụ đo lường coverage trong CI/CD pipeline |
| 2 | Concurrency Test | Không xảy ra lỗi âm kho hoặc lệch số dư khi chạy tải đồng thời | Chạy bộ test tích hợp API với 50 luồng giả lập |
| 3 | Security Validation | 100% API thay đổi dữ liệu (POST, PUT, DELETE) bắt buộc phải xác thực JWT | Quét tự động danh sách routes API |
| 4 | UI Responsiveness | Giao diện hiển thị đúng chuẩn, không vỡ layout trên độ phân giải máy trạm `1280x1024` | Chạy Playwright test viewport |
| 5 | Hardware Emulation | Web UI nhận dữ liệu quét từ máy cầm tay và dữ liệu cân điện tử dưới 100ms | Đo thời gian phản hồi logs của Local Agent |
