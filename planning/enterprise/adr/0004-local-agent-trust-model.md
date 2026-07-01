# ADR 0004: Mô hình tin cậy (Trust Model) cho Local Agent kết nối thiết bị ngoại vi

## Trạng thái
Đã duyệt (Approved)

## Bối cảnh
Hệ thống Nexustock WMS chạy trên trình duyệt Web (HTTPS) cần giao tiếp trực tiếp với các thiết bị phần cứng nằm tại nhà kho vật lý như:
1. **Cân điện tử:** Kết nối qua cổng nối tiếp (COM/RS-232).
2. **Máy in tem nhãn (Zebra, TSC):** Kết nối qua cổng USB hoặc cổng mạng nội bộ (ZPL/TSPL raw TCP port 9100).

Do chính sách bảo mật của trình duyệt web (CORS, Mixed-Content), trang web HTTPS từ internet không thể gọi trực tiếp đến cổng COM hoặc địa chỉ IP local của máy tính thủ kho.

Để giải quyết vấn đề này, chúng tôi thiết kế một phần mềm nhỏ chạy tại máy tính cục bộ của thủ kho đóng vai trò là cầu nối (**Local Agent**). Local Agent sẽ mở một WebSocket Server cục bộ và trình duyệt Web UI sẽ kết nối đến WebSocket này. Tuy nhiên, việc mở một cổng WebSocket cục bộ tiềm ẩn nhiều rủi ro bảo mật nghiêm trọng:
- Bất kỳ trang web độc hại nào người dùng truy cập cũng có thể gửi lệnh qua WebSocket này để in tem bừa bãi hoặc đọc trộm dữ liệu cân.
- Ransomware hoặc phần mềm độc hại trong mạng nội bộ có thể lợi dụng Local Agent để điều khiển thiết bị ngoại vi.

## Quyết định
Chúng tôi quyết định áp dụng **Mô hình tin cậy đa lớp (Multi-layered Trust Model)** cho Local Agent để bảo vệ an toàn cho kết nối giữa Web Cloud và thiết bị ngoại vi.

### Các biện pháp bảo mật cốt lõi:
1. **Chỉ bind địa chỉ Loopback (Localhost Only):**
   - WebSocket Server của Local Agent bắt buộc chỉ được lắng nghe tại địa chỉ `127.0.0.1:9000` (hoặc cổng cấu hình). Tuyệt đối cấm lắng nghe tại `0.0.0.0` để chặn mọi kết nối từ các máy tính khác trong mạng LAN.

2. **Xác thực Origin nghiêm ngặt (Origin Allowlist):**
   - Khi thiết lập kết nối WebSocket, Local Agent kiểm tra Header `Origin` do trình duyệt gửi lên.
   - Chỉ cho phép các kết nối đến từ domain được cấu hình trước (ví dụ: `https://app.nexustock.vn` hoặc `https://*.nexustock.vn`). Mọi Origin lạ hoặc rỗng (ngoại trừ môi trường dev `http://localhost:*` được bật tùy chọn) sẽ bị từ chối kết nối ngay lập tức.

3. **Cơ chế ghép cặp bảo mật (Pairing Token Workflow):**
   - Khi cài đặt Local Agent lần đầu tiên, Agent ở trạng thái chưa ghép cặp (`unpaired`).
   - Để ghép cặp:
     1. Thủ kho đăng nhập vào Web UI chính thức, chọn chức năng "Ghép cặp thiết bị". Web UI sẽ sinh ra một mã ghép cặp ngắn hạn (One-Time Pairing Code, hiệu lực 3 phút).
     2. Người dùng nhập Pairing Code này vào giao diện cài đặt cục bộ của Local Agent (hoặc bấm nút liên kết tự động để truyền qua WebSocket bắt tay).
     3. Local Agent gửi Pairing Code lên Web API để xác thực. Nếu hợp lệ, Web API trả về một mã định danh trạm (`stationId`) và một mã khóa phiên (`AgentToken`).
     4. Local Agent lưu trữ `AgentToken` và `stationId` vào bộ nhớ an toàn của hệ điều hành (Windows Credential Manager / DPAPI), không lưu dạng file text phẳng để tránh bị phần mềm độc hại đọc trộm.

4. **Bảo mật kết nối WSS & Certificate Trust:**
   - Trong môi trường production, kết nối bắt buộc phải sử dụng `wss://127.0.0.1:9000` (WebSocket Secure) để trình duyệt HTTPS không chặn mixed-content.
   - Trình cài đặt MSIX tự động tạo chứng chỉ SSL cục bộ cho `localhost` và thêm nó vào `Trusted Root Certification Authorities` trên Windows.
   - Hỗ trợ cơ chế quét cổng tự động từ `9000-9005` (Port Discovery) để tự động kết nối nếu cổng 9000 bị chiếm dụng.

5. **Xác thực WebSocket Message & Chống Replay:**
   - Mỗi tin nhắn gửi qua WebSocket từ Web UI đến Agent phải đính kèm chữ ký HMAC SHA-256 (sinh từ `AgentToken`) và `timestamp`.
   - Local Agent từ chối các tin nhắn có độ lệch thời gian (Time Skew) vượt quá 30 giây để chống tấn công phát lại (Replay Attack).

6. **Thu hồi quyền từ xa (Remote Revocation):**
   - Quản trị viên trên Web UI có thể bấm "Revoke" một trạm làm việc (Station). Hệ thống sẽ đánh dấu `AgentToken` của trạm đó là vô hiệu lực trong database. Trong lần kết nối hoặc heartbeat tiếp theo, Local Agent sẽ bị Web API từ chối và bắt buộc phải thực hiện ghép cặp lại từ đầu.

## Hệ quả & Đánh giá

### Ưu điểm (Benefits):
- **Bảo mật tuyệt đối:** Ngăn chặn hoàn toàn việc các trang web giả mạo tấn công và điều khiển thiết bị ngoại vi của kho.
- **Trải nghiệm người dùng tốt:** Sau khi ghép cặp lần đầu, các lần sử dụng sau kết nối tự động thiết lập mà không cần người dùng can thiệp lại.
- **Quản lý tập trung:** Quản trị viên có thể theo dõi danh sách các máy trạm đang kết nối và thu hồi quyền ngay lập tức nếu nghi ngờ máy trạm bị lộ thông tin.

### Nhược điểm & Cách giảm thiểu (Risks & Mitigations):
- **Lỗi kết nối SSL/WSS:** Trình duyệt HTTPS đôi khi chặn kết nối WebSocket không mã hóa (`ws://127.0.0.1`).
  - *Biện pháp giảm thiểu:* Cung cấp chứng chỉ SSL tự ký (Self-signed Certificate) cục bộ đi kèm với Local Agent để chạy `wss://127.0.0.1:9000`, hoặc hướng dẫn thủ kho cấu hình cho phép kết nối unsecured websocket cho địa chỉ localhost trên Chrome/Edge.

*ponytail: Trong bản phát hành chính thức, Local Agent sẽ được ký số (Code Signing) bằng chứng chỉ doanh nghiệp để tránh bị Windows SmartScreen cảnh báo và chặn cài đặt.*
