# Non-functional requirements (Yêu cầu phi chức năng cho hệ thống WMS)

Tài liệu định nghĩa các chỉ số chất lượng, bảo mật, hiệu năng và độ tin cậy bắt buộc cho hệ thống Nexustock WMS.

---

## 1. Hiệu năng & Khả năng đáp ứng (Performance & Responsiveness)

- **Thời gian phản hồi API (API Response Latency):**
  - Các API truy vấn thông thường (danh mục, danh sách): 95% số request phải hoàn tất dưới **200ms**.
  - Các API mutation nghiệp vụ (nhập kho, di chuyển, phân bổ): 95% số request phải hoàn tất dưới **500ms** (bao gồm cả ghi DB transaction và chạy Rule Engine).
  - API load danh sách tồn kho lớn (trên 10,000 dòng): Phải hoàn tất dưới **1000ms** thông qua phân trang và tối ưu hóa index.
- **Tốc độ phản hồi thiết bị cầm tay RF (RF/Mobile UI Responsiveness):**
  - Mọi thao tác quét barcode trên thiết bị cầm tay (handheld) phải trả kết quả (Thành công hoặc Lỗi rõ ràng) về màn hình dưới **300ms** trong điều kiện mạng Wifi nội bộ ổn định.
- **Giới hạn tải trọng dữ liệu (Batch Import Limits):**
  - Hệ thống cho phép import tối đa **1,000 dòng** dữ liệu Item/Location trên mỗi file Excel/CSV. Thời gian preview file không quá **3 giây**, thời gian commit ghi DB không quá **5 giây**.
- **Chính sách phân trang (Pagination Policy):**
  - Tất cả các API GET trả về danh sách bắt buộc phải hỗ trợ phân trang.
  - Page size mặc định là `20` dòng, page size tối đa được cấu hình là `100` dòng.

---

## 2. Độ tin cậy & Tính khả dụng (Reliability & Availability)

- **Chỉ số Uptime tối thiểu (System Uptime):**
  - Hệ thống Web Cloud và API Backend phải đạt độ sẵn sàng tối thiểu **99.9%** (Uptime) hàng tháng.
  - Hệ thống Local Agent chạy tại kho hàng phải tự động khôi phục kết nối WebSocket ngay khi có mạng trở lại mà không cần khởi động lại máy trạm.
- **Mục tiêu phục hồi dữ liệu (RPO & RTO):**
  - **Recovery Point Objective (RPO):** Tối đa **1 giờ** (tức là trong trường hợp xảy ra thảm họa phần cứng, dữ liệu khôi phục lại không bị mất quá 1 giờ gần nhất).
  - **Recovery Time Objective (RTO):** Tối đa **2 giờ** để dựng lại toàn bộ hệ thống hoạt động bình thường từ bản backup gần nhất.
- **Chính sách Sao lưu & Lưu trữ (Backup & Retention Policy):**
  - Database được backup tự động hàng ngày (Daily Full Backup) vào lúc 01:00 AM.
  - Lưu giữ bản backup trong vòng **30 ngày** trên cloud storage hoặc server vật lý dự phòng an toàn.
  - Bản ghi Audit Log thay đổi dữ liệu (`AuditLogs`) và lịch sử giao dịch tồn kho (`InventoryTransactions`) phải được lưu trữ tối thiểu **5 năm** phục vụ mục đích kiểm toán thuế và hải quan.

---

## 3. Bảo mật & An toàn thông tin (Security & Data Confidentiality)

- **Mật hóa dữ liệu (Encryption):**
  - Toàn bộ kết nối HTTP bắt buộc phải sử dụng SSL/TLS 1.3 (HTTPS).
  - Thông tin nhạy cảm của người dùng như mật khẩu bắt buộc phải băm bằng thuật toán an toàn (BCrypt hoặc Argon2id) trước khi lưu DB.
- **Bảo mật thiết bị đầu cuối (Local Agent Security):**
  - WebSocket của Local Agent chỉ được kết nối cục bộ (`127.0.0.1`).
  - Kiểm tra Origin của trình duyệt dựa trên allowlist cấu hình sẵn.
  - Token ghép cặp của Agent phải được lưu trữ bằng API mã hóa của hệ điều hành (Windows DPAPI).
- **Giới hạn tần suất gọi API (Rate Limiting):**
  - Áp dụng Rate Limiting cho API đăng nhập: Tối đa **5 lần đăng nhập sai** liên tiếp từ một IP trong vòng 15 phút sẽ bị khóa tạm thời 15 phút.
  - API nghiệp vụ chung: Tối đa **100 requests/phút** trên mỗi tài khoản User/Token tích hợp (API Key).
- **An toàn log (Secret Masking):**
  - Logs hệ thống tuyệt đối không được chứa: mật khẩu ở dạng rõ, mã pin, token khóa ghép cặp trạm, payload token JWT hoặc thông tin thẻ thanh toán.
  - Các thông số nhạy cảm trong payload API tích hợp phải được che mờ (masking) bằng dấu sao (`***`) trước khi ghi file log.

---

## 4. Khả năng giám sát & Vận hành (Observability & Supportability)

- **Cơ chế Trace ID:**
  - Mỗi request từ client gửi lên hệ thống được gán một mã `traceId` duy nhất tại API Gateway/Middleware.
  - `traceId` này bắt buộc phải đi kèm trong mọi dòng log hệ thống, log database transaction, log job nền và log lỗi trả về cho client.
- **Giám sát hoạt động (APM & Health check):**
  - API cung cấp hai endpoint kiểm tra trạng thái: `/health/live` (tiến trình đang sống) và `/health/ready` (các kết nối DB, Redis, máy in sẵn sàng).
  - Các endpoint này không được trả thông tin cấu hình nhạy cảm và được giám sát bởi hệ thống monitoring bên ngoài để tự động gửi cảnh báo (Telegram/Slack/Email) cho đội vận hành WMS khi có sự cố.
