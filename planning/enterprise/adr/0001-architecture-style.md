# ADR 0001: Lựa chọn phong cách kiến trúc Modular Monolith

## Trạng thái
Đã duyệt (Approved)

## Bối cảnh
Hệ thống Nexustock WMS thế hệ mới cần thay thế hệ thống desktop cũ. Yêu cầu đặt ra là hệ thống phải chạy ổn định trên môi trường local của kho hàng (on-premise VM hoặc server vật lý) cũng như có khả năng mở rộng lên Cloud để vận hành multi-tenant cho nhiều công ty khác nhau. 

Một số ý kiến đề xuất sử dụng kiến trúc Microservices ngay từ đầu để dễ mở rộng độc lập các service như Inbound, Outbound, Inventory, Rule Engine. Tuy nhiên, điều này đi kèm với các thách thức lớn về:
1. Độ phức tạp trong việc quản lý giao dịch phân tán (Distributed Transactions - Saga Pattern) khi thực hiện nghiệp vụ kho (ví dụ: cất hàng liên quan đến cả Inbound, Inventory và Rule Engine).
2. Chi phí triển khai và vận hành hạ tầng quá lớn đối với máy chủ local tại kho hàng (EDR, RAM/CPU giới hạn).
3. Khó khăn trong việc phát triển, debug và kiểm thử cục bộ cho các lập trình viên.

## Quyết định
Chúng tôi quyết định chọn **Modular Monolith** làm phong cách kiến trúc chủ đạo cho phần Backend của Nexustock.

### Nguyên tắc thiết kế Modular Monolith:
1. **Chia tách Module logic rõ ràng:** Toàn bộ mã nguồn backend được gom chung vào một repository (Monorepo), nhưng chia thành các module nghiệp vụ độc lập (ví dụ: `Nexustock.Modules.MasterData`, `Nexustock.Modules.Inbound`, `Nexustock.Modules.Inventory`, `Nexustock.Modules.Outbound`, `Nexustock.Modules.Identity`).
2. **Cô lập Database Schema:** Mỗi module sở hữu các bảng dữ liệu riêng. Chỉ truy cập database của module khác thông qua API nội bộ (Interface/Service) hoặc Event-driven (Mediator/Inbox-Outbox), không join bảng trực tiếp xuyên module ở tầng database.
3. **Giao tiếp bất đồng bộ:** Giữa các module, ưu tiên giao tiếp thông qua sự kiện (Integration Events) bằng thư viện nội bộ (In-memory Mediator như MediatR) để giảm liên kết cứng (Loose Coupling).
4. **Một database vật lý duy nhất:** Sử dụng chung một cơ sở dữ liệu PostgreSQL nhưng chia Logical Schema (`master_data`, `inbound`, `inventory`, `outbound`, `identity`) để sẵn sàng tách database vật lý sau này nếu cần.

## Hệ quả & Đánh giá

### Ưu điểm (Benefits):
- **Đơn giản hóa giao dịch (ACID Transactions):** Các tác vụ nghiệp vụ phức tạp đòi hỏi tính toàn vẹn dữ liệu cao (như nhận hàng, trừ tồn kho và ghi ledger) có thể thực thi trong cùng một Database Transaction mà không lo mất đồng bộ dữ liệu.
- **Tiết kiệm tài nguyên:** Dễ dàng chạy trên 1 Server/Container nhỏ tại các kho hàng cục bộ (phù hợp với môi trường on-premise giới hạn tài nguyên).
- **Tốc độ phát triển nhanh:** Dev dễ dàng debug end-to-end trên máy cá nhân mà không cần dựng cụm Kubernetes hoặc chạy hàng chục service độc lập.
- **Lộ trình nâng cấp mượt mà:** Nếu một module (như Rule Engine hoặc Inventory) bị quá tải trong tương lai, việc bóc tách module đó ra thành một Microservice độc lập rất dễ dàng vì các bảng dữ liệu và logic code đã được cô lập từ trước.

### Nhược điểm & Cách giảm thiểu (Risks & Mitigations):
- **Nguy cơ vi phạm ranh giới module (Monolith Spaghetti):** Dev có thể vô tình gọi trực tiếp class/DB của module khác qua dependency injection.
  - *Biện pháp giảm thiểu:* Sử dụng ArchUnit .NET để viết unit test kiểm tra ranh giới kiến trúc (Architectural Invariants). Bất kỳ code nào gọi sai module sẽ bị fail build ngay ở local.
- **Single Point of Failure (SPOF):** Một module bị crash (ví dụ: rò rỉ bộ nhớ ở module in ấn) có thể làm sập toàn bộ ứng dụng.
  - *Biện pháp giảm thiểu:* Phân tách luồng xử lý nặng sang các background worker độc lập hoặc dùng các cơ chế cô lập luồng (Thread isolation).

## Các phương án thay thế đã cân nhắc
- **Microservices:** Bị loại bỏ vì chi phí vận hành mạng và quản lý transaction phân tán quá cao ở giai đoạn MVP.
- **Spaghetti Monolith (Kiến trúc truyền thống không chia module):** Bị loại bỏ vì khó bảo trì, code bị phụ thuộc chéo lẫn nhau, khi đổi một cột trong DB có thể làm hỏng toàn bộ hệ thống mà không kiểm soát được ảnh hưởng downstream.

*ponytail: Modular Monolith là giải pháp tối ưu cho giai đoạn 1-3 năm đầu. Nếu quy mô tăng lên trên 100 kho hoạt động đồng thời, module Inventory Ledger và Allocation sẽ được tách thành các dịch vụ microservice riêng.*
