# ADR 0005: Mô hình đảm bảo độ tin cậy tích hợp (Integration Reliability)

## Trạng thái
Đã duyệt (Approved)

## Bối cảnh
Hệ thống Nexustock WMS không hoạt động độc lập mà phải liên kết chặt chẽ với các hệ thống khác:
1. **ERP Downstream:** Đồng bộ dữ liệu phiếu nhập, phiếu xuất, trạng thái hoàn tất đơn hàng.
2. **Hệ thống Webhook:** Gửi thông báo sự kiện thời gian thực cho các bên thứ ba (ví dụ: thông báo cho hệ thống CRM khi hàng đã xuất kho).

Các kết nối mạng kết nối đến hệ thống ngoài luôn có rủi ro bị chập chờn, mất mạng, hoặc server của đối tác bị sập tạm thời. Nếu hệ thống gọi API trực tiếp của đối tác ngay trong luồng nghiệp vụ chính của WMS:
- Nếu API đối tác bị chậm, luồng nghiệp vụ của WMS sẽ bị nghẽn (Block), thủ kho không thể bấm xác nhận trên màn hình.
- Nếu API đối tác bị lỗi (500, Timeout), WMS có nguy cơ bị rollback dữ liệu (mặc dù hàng đã xuất thực tế trong kho), hoặc dữ liệu giữa hai bên bị lệch (WMS ghi nhận đã xuất nhưng ERP không nhận được tin).

## Quyết định
Chúng tôi quyết định áp dụng **Mô hình Tích hợp Tin cậy Đa tầng (Multi-layered Reliability Model)** dựa trên hai mẫu thiết kế chính: **Transactional Outbox Pattern** cho việc xuất dữ liệu và **Retry with Backoff + Dead-Letter Queue (DLQ)** cho webhook.

### Chi tiết giải pháp tích hợp:
1. **Transactional Outbox Pattern (Gửi tin cậy):**
   - Khi một sự kiện nghiệp vụ xảy ra (ví dụ: hoàn tất xuất kho), thay vì gọi trực tiếp API của ERP hoặc gửi Webhook ngay lập tức, hệ thống sẽ chèn một bản ghi tin nhắn vào bảng `IntegrationOutbox` trong cùng Database Transaction của nghiệp vụ đó.
   - Bản ghi Outbox chứa: `id`, `tenantId`, `eventType`, `payload` (JSON dữ liệu), `status` (PENDING, PROCESSING, SENT, FAILED), `retryCount`, `nextAttemptAt`, `traceId`.
   - Một Job chạy nền (Background Worker) quét bảng `IntegrationOutbox` theo chu kỳ (ví dụ: mỗi 2 giây), lấy các tin nhắn `PENDING` hoặc đến lịch retry để gửi đi.
   - Sau khi gửi thành công, trạng thái tin nhắn cập nhật thành `SENT`. Nếu thất bại, cập nhật `FAILED` và lên lịch retry.

2. **Cơ chế Retry với Exponential Backoff & Jitter:**
   - Khi gửi tin nhắn thất bại do lỗi mạng hoặc lỗi server đối tác (5xx, Timeout), hệ thống sẽ tự động thử lại sau các khoảng thời gian tăng dần: 1 phút, 5 phút, 15 phút, 1 giờ, 6 giờ.
   - Thêm một lượng thời gian ngẫu nhiên nhỏ (Jitter) vào khoảng chờ để tránh hiện tượng tất cả các tin nhắn bị lỗi đồng loạt gọi lại hệ thống đối tác cùng một lúc gây nghẽn mạng (Thundering Herd Problem).

3. **Dead-Letter Queue (DLQ) & Cơ chế Replay thủ công:**
   - Nếu tin nhắn vượt quá số lần thử lại tối đa (ví dụ: 5 lần), trạng thái tin nhắn sẽ chuyển thành `DEAD_LETTER`. Hệ thống sẽ gửi cảnh báo (Alert) cho quản trị viên.
   - Quản trị viên sau khi liên hệ đối tác để sửa lỗi hệ thống của họ có thể vào giao diện Admin của Nexustock, bấm nút "Replay" (gửi lại) cho các tin nhắn nằm trong DLQ.

4. **Chống trùng lặp tin nhắn phía nhận (Idempotency Key):**
   - Mọi tin nhắn gửi đi từ Nexustock sang đối tác và ngược lại bắt buộc phải đính kèm `Idempotency-Key` (hoặc `messageId` độc nhất).
   - Hệ thống nhận (bao gồm cả Nexustock khi nhận dữ liệu ERP) phải lưu vết các key đã xử lý. Nếu nhận được tin nhắn trùng key, hệ thống trả về kết quả đã xử lý trước đó mà không thực hiện lại các thao tác ghi database hoặc thay đổi tồn kho lần hai.

5. **Ký số bảo mật Webhook (HMAC SHA-256):**
   - Để đảm bảo tin nhắn webhook không bị giả mạo trên đường truyền, Nexustock ký payload bằng thuật toán HMAC SHA-256 với một khóa bí mật (Secret Key) dùng chung cho từng client. Chữ ký được gửi kèm trong Header `X-Nexustock-Signature`.
   - Hệ thống nhận bắt buộc phải tự tính lại chữ ký và đối chiếu trước khi xử lý tin nhắn.

## Hệ quả & Đánh giá

### Ưu điểm (Benefits):
- **Độ tin cậy tuyệt đối (Guaranteed Delivery):** Đảm bảo tin nhắn tích hợp luôn được gửi đến đích thành công ít nhất một lần (At-least-once Delivery).
- **Cô lập lỗi (Fault Isolation):** Lỗi của hệ thống bên ngoài không bao giờ làm gián đoạn các thao tác trực tiếp của thủ kho tại nhà kho vật lý.
- **Tính nhất quán dữ liệu cao:** Loại bỏ hoàn toàn hiện tượng lệch số liệu giữa ERP và WMS khi có sự cố mạng.

### Nhược điểm & Cách giảm thiểu (Risks & Mitigations):
- **Trễ thời gian thực (Eventual Consistency):** Dữ liệu không được đồng bộ sang ERP ngay lập tức mà có thể trễ vài giây (hoặc vài giờ nếu mất mạng).
  - *Biện pháp giảm thiểu:* Thiết lập độ ưu tiên gửi cho Outbox Job. Các tin nhắn quan trọng (như xác nhận xuất kho) được đẩy lên đầu hàng đợi gửi trước.

*ponytail: Trong tương lai, nếu tần suất tích hợp vượt quá 500,000 tin nhắn/ngày, chúng tôi sẽ chuyển từ quét bảng database Outbox sang sử dụng hàng đợi Message Queue chuyên dụng như RabbitMQ hoặc Kafka.*
