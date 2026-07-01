# Risk register (Danh mục quản lý rủi ro dự án WMS)

Hồ sơ quản lý rủi ro cho quá trình triển khai và vận hành hệ thống Nexustock WMS.

## Phân loại mức độ rủi ro (Risk Matrix)

- **Severity (Tác động):** 1 (Thấp) -> 5 (Thảm họa - mất dữ liệu, sập kho)
- **Probability (Khả năng xảy ra):** 1 (Hiếm gặp) -> 5 (Hầu như chắc chắn)
- **Risk Score:** Severity × Probability (Score >= 12 là High/Critical, cần biện pháp giảm thiểu lập tức)

---

## Danh mục rủi ro

| ID | Tên rủi ro & Mô tả | Phase | Sev | Prob | Score | Biện pháp phòng ngừa (Mitigation) | Cơ chế phát hiện (Detection) | Chủ trì (Owner) |
|---|---|---|---|---|---|---|---|---|
| **R-01** | **Âm tồn kho vật lý (Negative Inventory)**<br>Số dư tồn kho bị trừ âm do lỗi tranh chấp ghi (Concurrency) hoặc logic code sai. | Phase 06 | 5 | 3 | **15** (High) | - Áp dụng DB check constraint `qty >= 0` ở mức table.<br>- Sử dụng Optimistic Concurrency Control (OCC) qua `rowVersion`. | Integration tests quét số dư âm; Alert lập tức khi có DB exception vi phạm constraint. | Tech Lead / Database Admin |
| **R-02** | **Mất mát dữ liệu khi ERP tích hợp gián đoạn**<br>API ERP bị sập dẫn đến việc gửi thông báo xuất/nhập kho bị mất. | Phase 23, 24 | 4 | 4 | **16** (High) | - Áp dụng Transactional Outbox Pattern.<br>- Lưu trạng thái tin nhắn chưa gửi và tự động retry với Exponential Backoff. | Dashboard theo dõi số lượng tin nhắn lỗi trong Outbox; Alert khi tin nhắn vào DLQ. | Integration Dev |
| **R-03** | **Local Agent bị hack hoặc điều khiển trái phép**<br>Trang web độc hại truy cập WebSocket cục bộ để in nhãn hoặc đọc cân. | Phase 20 | 4 | 3 | **12** (High) | - Chỉ bind loopback `127.0.0.1`.<br>- Kiểm tra CORS Origin Allowlist nghiêm ngặt.<br>- Bắt buộc Pairing Token handshake. | Local Agent log ghi nhận các kết nối bị reject do sai Origin/Token. | Security Specialist |
| **R-04** | **Lỗi in trùng tem nhãn hoặc in sai mã**<br>Máy in tem nhận lệnh chậm, thủ kho ấn in lại nhiều lần dẫn đến dán sai nhãn lên pallet. | Phase 22 | 4 | 3 | **12** (High) | - Enforce `Idempotency-Key` cho mỗi Print Job.<br>- Phân quyền chặt tác vụ in lại (Reprint), yêu cầu lý do và audit log. | Hệ thống log kiểm tra tỷ lệ reprint bất thường trên một mã đơn. | QA Lead / Ops Supervisor |
| **R-05** | **Cân điện tử bị chập chờn, nhiễu số**<br>Cổng COM gửi dữ liệu rác hoặc nhảy số liên tục khiến thủ kho không đóng được carton. | Phase 21 | 3 | 4 | **12** (High) | - Thuật toán ổn định số cân: chỉ chấp nhận cân nặng khi sai số giữa N lần đọc liên tiếp nằm trong dung sai cho phép.<br>- Cung cấp màn hình nhập tay có kiểm soát (Manual Fallback) yêu cầu lý do. | Log ghi nhận số lần thủ kho phải ghi đè cân tay (manual override weight). | Local Agent Dev |
| **R-06** | **Nghẽn hiệu năng khi phân bổ lô lớn (Allocation)**<br>Đơn xuất có hàng ngàn dòng cần chạy Rule Engine chọn lô FEFO làm khóa DB. | Phase 13 | 4 | 2 | **8** (Medium) | - Đọc dữ liệu cân bằng (Balance) và chạy thuật toán phân bổ trong bộ nhớ, chỉ ghi nhận khóa dòng (Lock) ở bước cuối.<br>- Pagination cho đơn hàng lớn. | APM (Application Performance Monitoring) đo thời gian chạy API `/allocate`. | Backend Dev |
| **R-07** | **Lệch tồn kho kiểm kê do không khóa vị trí**<br>Thủ kho đang đếm hàng tại vị trí A nhưng xe nâng vẫn chuyển hàng đi chỗ khác. | Phase 08 | 4 | 3 | **12** (High) | - Trạng thái `Locked` vị trí khi tạo phiếu kiểm kê (Cycle Count Task).<br>- Chặn mọi giao dịch di chuyển, xuất hàng từ vị trí đang bị khóa. | API di chuyển trả lỗi `location.locked` nếu cố tình thao tác. | Product Owner |
| **R-08** | **Mất mạng internet tại nhà kho vật lý**<br>Nhà kho bị đứt cáp quang, Web Cloud không thể truy cập được. | Phase 30 | 5 | 2 | **10** (Medium) | - Có đường truyền 4G backup tự động định tuyến tại router kho.<br>- Hướng dẫn thủ kho quy trình ghi chép giấy tạm thời và nhập bù khi có mạng (offline runbook). | Hệ thống giám sát ping (Uptime Kuma) từ cloud về IP gateway của kho. | Network Admin / Ops Lead |
| **R-09** | **Lỗi nâng cấp Database làm hỏng dữ liệu (Migration Fail)**<br>Deploy phiên bản mới nhưng script migration bị lỗi giữa chừng. | Phase 26 | 5 | 2 | **10** (Medium) | - Bắt buộc backup database tự động trước khi chạy migration.<br>- Script migration phải được viết cả hai chiều Up và Down (Rollback plan). | Logs chạy Docker compose deployment. | DevOps Engineer |
| **R-10** | **Quét sai mã vạch (Barcode mismatch)**<br>Thủ kho quét mã nhà sản xuất nhưng hệ thống không nhận diện được do chưa khai báo alias. | Phase 09 | 3 | 3 | **9** (Medium) | - Hỗ trợ bảng alias barcode cho Item.<br>- RF UI hiển thị thông báo lỗi âm thanh lớn và màn hình đỏ lòe để cảnh báo quét sai. | Log ghi nhận các barcode quét không thành công (unknown barcode). | RF Frontend Dev |
