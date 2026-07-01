# PHASE 24: Webhook & integration reliability

## Execution spec maturity

- **Mức hiện tại:** 88%
- **Đánh giá:** Đủ direction cho webhook reliability, retry, backoff, DLQ và replay.
- **Khi cần upgrade:** Upgrade nếu đối tác yêu cầu SLA delivery, signing scheme hoặc replay window đặc thù.

## 1. Mục tiêu

Xây dựng hệ thống gửi tin Webhook và cơ chế tích hợp tin cậy (Integration Reliability). Đảm bảo các thông báo sự kiện kho (như xuất kho thành công, nhập kho hoàn tất) luôn được chuyển đến bên thứ ba thành công ít nhất một lần (At-least-once Delivery) thông qua Outbox Pattern, cơ chế tự động thử lại (Retry with Backoff), hàng đợi tin lỗi (Dead-Letter Queue - DLQ), và tính năng gửi lại (Replay) thủ công.

## 2. Phạm vi

### In scope

- Xây dựng bảng đăng ký nhận tin Webhook (`WebhookSubscriptions`) cô lập theo Tenant.
- Triển khai cơ chế Transactional Outbox Pattern: Đảm bảo chèn bản ghi Outbox và thay đổi nghiệp vụ kho nằm trong một Database Transaction.
- Xây dựng Background Worker (Outbox Worker) quét và phát đi các sự kiện Webhook.
- Ký bảo mật nội dung tin nhắn gửi đi bằng thuật toán HMAC SHA-256.
- Áp dụng chính sách Retry tự động với Exponential Backoff & Jitter cho các lỗi mạng/HTTP lỗi tạm thời.
- Chuyển tiếp các tin nhắn thất bại liên tục vào Dead-Letter Queue (DLQ) để xử lý thủ công.

### Non-negotiable output

- Sự kiện kho (như `shipment.confirmed`) tự động kích hoạt tạo dòng Outbox tương ứng.
- Webhook gửi đi đính kèm chữ ký bảo mật ở Header `X-Nexustock-Signature`.
- Giao diện Admin quản trị có thể theo dõi tỷ lệ gửi lỗi, xem danh mục DLQ và thực hiện Replay (gửi lại) từng tin nhắn hoặc hàng loạt.

## 3. Điều kiện đầu vào

### Readiness checklist

- Module ERP integration contract (Phase 23) đã định nghĩa.
- Hệ thống log tập trung (Phase 25) đã có khung mẫu.

## 4. Setup

### Cấu trúc module đề xuất

- Backend module: `backend/modules/webhook_reliability/`
- Background worker: `backend/workers/WebhookOutboxWorker/`
- Frontend module: `frontend/features/webhook_reliability/`

### Permission seed đề xuất

- `webhook.manage`: Đăng ký, sửa cấu hình URL nhận tin Webhook.
- `webhook.replay`: Thực hiện replay các tin nhắn lỗi trong DLQ.

## 5. Database

### Bảng đăng ký nhận tin Webhook (`WebhookSubscriptions`)

| Tên cột | Kiểu dữ liệu | Nullable | Ràng buộc chính | Ý nghĩa |
|---|---|---|---|---|
| `id` | uuid | No | Primary Key | ID đăng ký |
| `tenantId` | varchar(50) | No | FK | Định danh tenant |
| `targetUrl` | varchar(255) | No | | URL nhận webhook |
| `secretKey` | varchar(100) | No | | Khóa bí mật dùng để ký HMAC |
| `eventTypes` | varchar(250) | No | | Chuỗi chứa các sự kiện đăng ký (ví dụ: `shipment.*,inbound.completed`) |
| `isActive` | boolean | No | Mặc định: true | Trạng thái hoạt động |

### Bảng hàng đợi gửi tin Webhook (`WebhookDeliveries` / Outbox)

| Tên cột | Kiểu dữ liệu | Nullable | Ràng buộc chính | Ý nghĩa |
|---|---|---|---|---|
| `id` | uuid | No | Primary Key | ID lần gửi |
| `tenantId` | varchar(50) | No | FK | Định danh tenant |
| `subscriptionId`| uuid | No | FK | Liên kết subscription |
| `eventType` | varchar(50) | No | | Loại sự kiện phát sinh |
| `payload` | text | No | | JSON body dữ liệu tin nhắn |
| `status` | varchar(20) | No | | Trạng thái: `pending`, `sending`, `delivered`, `deadLetter` |
| `retryCount` | integer | No | Mặc định: 0 | Số lần đã thử lại |
| `nextAttemptAt` | timestamp | No | | Lịch thử lại kế tiếp |
| `traceId` | varchar(50) | No | | Trace ID liên kết |
| `lastResponseCode`| integer | Yes | | HTTP code phản hồi gần nhất |
| `lastError` | text | Yes | | Lỗi kết nối gần nhất |
| `createdAt` | timestamp | No | | Thời gian tạo sự kiện |

## 6. Backend/API

### 6.1 API Đăng ký Webhook mới
- **Method & Path:** `POST /api/webhooks/subscriptions`
- **Permission:** `webhook.manage`
- **Request:**
  ```json
  {
    "targetUrl": "https://api.erp-customer.com/wms-receiver",
    "eventTypes": "shipment.confirmed,inbound.completed"
  }
  ```
- **Response (Success):** `{ "subscriptionId": "uuid-sub-11", "secretKey": "whsec_abc123xyz" }`
- *Ghi chú:* Hệ thống tự sinh `secretKey` ngẫu nhiên có độ dài entropy tối thiểu 32 ký tự.

### 6.2 API Gửi lại tin nhắn lỗi (Replay Webhook Delivery)
- **Method & Path:** `POST /api/webhooks/deliveries/{id}/replay`
- **Permission:** `webhook.replay`
- **Response (Success):** `{ "success": true, "status": "pending", "nextAttemptAt": "2026-07-01T09:30:00Z" }`
- *Ghi chú:* Chuyển trạng thái bản ghi từ `deadLetter` về `pending` và reset `retryCount = 0` để Outbox Worker quét và gửi lại.

## 7. Frontend/RF/mobile

### Màn hình Webhook Admin (Webhook Logs & DLQ UI)
- Cho phép xem danh sách các Subscription hiện có, xem biểu đồ tỷ lệ gửi tin lỗi theo thời gian.
- Trang chi tiết hiển thị toàn bộ Log lịch sử gửi (`WebhookDeliveries`) kèm payload JSON, HTTP Response Code, số lần retry.
- Bảng hiển thị riêng các tin đang nằm trong DLQ (`status = 'deadLetter'`) kèm nút bấm "Gửi lại" (Replay).

## 8. Execution flow

### Quy trình tạo và ký Webhook (HMAC Signature Process)

1. Nghiệp vụ kho hoàn tất (ví dụ: xác nhận xuất kho) -> Chèn bản ghi Outbox vào bảng `WebhookDeliveries` trong cùng DB transaction.
2. Background Job quét các bản ghi có `status = 'pending'` hoặc `nextAttemptAt <= CURRENT_TIMESTAMP`.
3. Chuẩn bị payload và ký số:
   - Đọc `secretKey` từ subscription liên quan.
   - Tính toán chữ ký HMAC SHA-256: `signature = HMAC-SHA256(secretKey, timestamp + "." + payload)`.
4. Gửi HTTP POST request đến `targetUrl` đính kèm các Header:
   - `X-Nexustock-Event`: `shipment.confirmed`
   - `X-Nexustock-Delivery-Id`: `id` của bản ghi gửi.
   - `X-Nexustock-Timestamp`: Timestamp thời điểm gửi.
   - `X-Nexustock-Signature`: Chữ ký HMAC đã tính.
5. Xử lý kết quả trả về từ URL đối tác:
   - Nếu HTTP Response trả về dạng `2xx` -> Cập nhật `status = 'delivered'`.
   - Nếu lỗi mạng hoặc HTTP `428/429/5xx` -> Tăng `retryCount`, tính toán `nextAttemptAt` theo chính sách Exponential Backoff (1m, 5m, 15m, 1h, 6h).
   - Nếu số lần thử lại vượt quá 5 lần -> Cập nhật `status = 'deadLetter'` và gửi Alert.

## 9. Validation & business rules

- **Bảo mật Webhook:** Bên nhận webhook bắt buộc phải xác thực tính hợp lệ của Header `X-Nexustock-Signature` để đảm bảo tin nhắn không bị thay đổi hoặc giả mạo.
- **Idempotency bên nhận:** WMS bắt buộc truyền `X-Nexustock-Delivery-Id` làm khóa Idempotency duy nhất để bên nhận không xử lý đơn trùng lặp khi WMS gửi lại tin do timeout.

## 10. Exception handling

- **Đổi địa chỉ URL hoặc IP bị chặn:** Nếu đối tác thay đổi DNS hoặc IP và gây lỗi kết nối liên tục, job sẽ thử lại theo lịch và tự động chuyển vào DLQ sau khi hết số lượt retry.
- **Tránh nghẽn hàng đợi (Queue Isolation):** Các tin nhắn lỗi liên tục không được làm chặn đường truyền của các tin nhắn mới. Do đó, chỉ quét các tin có `nextAttemptAt` hợp lệ, các tin chưa đến lịch retry sẽ bị bỏ qua để xử lý sau.

## 11. Observability

- Ghi log chi tiết mỗi lượt gửi: `[Webhook Outbox] Sending event {eventType} to {url} - Trace ID: {traceId}`.
- KPI giám sát: Tỷ lệ webhook gửi thành công lần đầu (First-pass Success Rate), số lượng tin nhắn trong DLQ.

## 12. Test plan

- **Unit Test:**
  - Logic tính chữ ký HMAC SHA-256 chính xác.
  - Thuật toán tính Exponential Backoff thời gian chờ tăng dần.
- **Integration Test:**
  - Kích hoạt sự kiện nghiệp vụ kho -> Verify dòng dữ liệu Outbox được tạo chính xác cùng transaction.
  - Sử dụng Webhook.site hoặc mock server để nhận webhook từ WMS -> Verify nhận đủ Headers và chữ ký chính xác.

## 13. Acceptance criteria

- Sự kiện kho phát sinh tự động tạo dòng Outbox tương ứng.
- Webhook được ký HMAC đầy đủ và gửi thành công sang mock server.
- Lỗi kết nối giả lập tự kích hoạt retry theo đúng khoảng thời gian cấu hình và chuyển vào DLQ sau khi hết lượt.

