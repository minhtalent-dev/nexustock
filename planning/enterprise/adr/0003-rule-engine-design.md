# ADR 0003: Thiết kế Rule Engine cho nghiệp vụ WMS

## Trạng thái
Đã duyệt (Approved)

## Bối cảnh
Hệ thống WMS cần đưa ra các quyết định tự động phức tạp như:
1. **Putaway Rule (Cất hàng):** Tìm vị trí cất hàng tối ưu dựa trên Zone, loại hàng, kích thước, nhiệt độ bảo quản, và sức chứa hiện tại của vị trí.
2. **Allocation Rule (Phân bổ hàng xuất):** Lấy hàng từ vị trí nào trước dựa trên các chiến lược FIFO (nhập trước xuất trước), FEFO (hạn dùng trước xuất trước), LIFO (nhập sau xuất trước), hoặc ưu tiên xuất theo thùng nguyên (LPN) trước lẻ sau.
3. **Replenishment Rule (Bổ sung hàng):** Tự động phát sinh yêu cầu di chuyển hàng từ kho lưu trữ (Reserve Zone) về khu vực lấy hàng lẻ (Pick Face Zone) khi tồn kho khả dụng tại Pick Face xuống dưới mức tối thiểu (Min Qty).

Nếu hardcode các logic này trong code backend, hệ thống sẽ rất khó tùy biến khi triển khai cho các kho hàng có đặc thù vận hành khác nhau (ví dụ: kho dược phẩm ưu tiên FEFO ngặt nghèo, kho linh kiện điện tử ưu tiên FIFO).

## Quyết định
Chúng tôi quyết định chọn thiết kế **Table-driven Rule Engine với cơ chế ưu tiên (Priority), điều kiện (Conditions), hành động (Actions) và nhật ký thực thi (Execution Logs)**.

### Chi tiết thiết kế Rule Engine:
1. **Cấu trúc Dữ liệu cấu hình Rule (`RuleDefinitions`):**
   - Lưu trữ các quy tắc dưới dạng bảng cấu hình trong database.
   - Các trường chính: `id`, `tenantId`, `ruleType` (PUTAWAY, ALLOCATION, REPLENISHMENT), `priority` (số nguyên, số nhỏ ưu tiên chạy trước), `ruleName`, `isActive` (flag bật/tắt rule).
   - **Conditions (Điều kiện áp dụng):** Lưu dưới dạng JSON chứa danh sách các tiêu chí lọc (ví dụ: `itemId = 'ITEM-001'`, `itemGroup = 'PHARMA'`, `zoneType = 'COOL'`, `partnerId = 'SUPPLIER-X'`).
   - **Actions (Hành động thực thi):** Lưu dưới dạng JSON chứa tham số cho chiến lược (ví dụ: `strategy = 'FEFO'`, `preferredZoneId = 'ZONE-A'`, `fallbackZoneId = 'ZONE-B'`, `allowPartial = false`).

2. **Cách thức Engine hoạt động (Execution Flow):**
   - Khi có sự kiện kích hoạt (ví dụ: yêu cầu phân bổ đơn hàng xuất):
     1. Hệ thống truy vấn toàn bộ các rule đang hoạt động (`isActive = true`) thuộc `ruleType` tương ứng của tenant, sắp xếp theo `priority` tăng dần.
     2. Engine duyệt qua từng rule, phân tích chuỗi JSON `Conditions` và đối chiếu với dữ liệu đầu vào (Context) của tác vụ.
     3. Rule đầu tiên thỏa mãn tất cả các điều kiện sẽ được chọn. Engine lập tức dừng duyệt và thực thi cấu hình trong phần `Actions` của rule đó.
     4. Nếu duyệt hết danh sách mà không có rule nào khớp, hệ thống sẽ tự động sử dụng **System Default Rule** (luôn có sẵn với priority lớn nhất, điều kiện rỗng).

3. **Nhật ký thực thi (`RuleExecutionLogs`):**
   - Mọi quyết định của Rule Engine bắt buộc phải ghi log để hỗ trợ kiểm tra và gỡ lỗi.
   - Các trường ghi log: `id`, `tenantId`, `traceId`, `ruleId` (rule được chọn), `inputContext` (JSON đầu vào), `matchedResult` (JSON kết quả/quyết định đưa ra), `executionTimeMs`.

## Hệ quả & Đánh giá

### Ưu điểm (Benefits):
- **Tính linh hoạt cực cao:** Người quản trị hệ thống có thể thay đổi chiến lược xuất hàng hoặc cất hàng trực tiếp trên giao diện quản trị bằng cách thêm/sửa/xóa hoặc đổi độ ưu tiên của rule mà không cần build/deploy lại code backend.
- **Dễ bảo trì và mở rộng:** Dev dễ dàng thêm các tiêu chí điều kiện mới vào JSON schema mà không làm ảnh hưởng đến các rule hiện có.
- **Minh bạch thông tin (Explainability):** Khi thủ kho thắc mắc "tại sao hệ thống lại gợi ý cất hàng vào vị trí A mà không phải vị trí B", admin chỉ cần tìm `RuleExecutionLogs` theo `traceId` để xem chi tiết rule nào đã khớp và điều kiện nào đã kích hoạt quyết định đó.

### Nhược điểm & Cách giảm thiểu (Risks & Mitigations):
- **Ảnh hưởng hiệu năng (Performance overhead):** Việc parse và đánh giá biểu thức logic từ JSON cho hàng trăm mặt hàng trong đơn hàng lớn có thể gây chậm.
  - *Biện pháp giảm thiểu:* Cache toàn bộ danh sách `RuleDefinitions` đang hoạt động vào bộ nhớ (In-memory cache hoặc Redis). Chỉ xóa cache và load lại khi admin thực hiện cập nhật rule trên UI.

*ponytail: Trong tương lai, nếu các điều kiện logic trở nên quá phức tạp vượt quá cấu hình JSON phẳng, chúng tôi sẽ tích hợp thư viện RulesEngine của Microsoft (C#) để biên dịch động các biểu thức logic thay vì tự viết parser.*
