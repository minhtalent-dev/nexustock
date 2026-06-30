# KẾ HOẠCH TỔNG THỂ TRIỂN KHAI DỰ ÁN NEXUSTOCK

Dự án **Nexustock** là giải pháp hệ thống quản lý kho tích hợp thế hệ mới, thay thế và nâng cấp toàn bộ các hệ thống Desktop cũ và tích hợp hệ thống quản lý kho chính hiện tại thành một nền tảng Web SPA hiện đại, chạy trên một cơ sở dữ liệu **PostgreSQL** độc lập hoàn toàn, hỗ trợ quy trình vận hành linh hoạt (Flexible Flow), hệ thống phân quyền chi tiết (RBAC), kiểm kê định kỳ tự động, phân hoạch Vùng kho, thuật toán cất hàng tối ưu (Slotting), quy trình chuyển tiếp trực tiếp (Cross-Docking), đợt gom hàng xuất (Wave Picking), cây gia phả truy vết chất lượng (Material Genealogy), đo lường năng suất lao động (Labor Tracking), lịch hẹn bến bãi (Dock Scheduling) và tích hợp cân điện tử tự động cho tất cả các nhà máy/chi nhánh sau này.

---

## 📅 LỘ TRÌNH TRIỂN KHAI CHI TIẾT (PROJECT ROADMAP)

Lộ trình được chia làm 7 Phase độc lập, có tài liệu hướng dẫn kỹ thuật chi tiết đi kèm cho từng giai đoạn:

```mermaid
gantt
    title Lộ trình triển khai Nexustock
    dateFormat  YYYY-MM-DD
    section Chuẩn bị & Thiết kế
    Phase 1: Setup Môi trường & Monorepo :active, p1, 2026-07-01, 3d
    Phase 2: Thiết kế DB PostgreSQL & RBAC  : p2, after p1, 4d
    section Phát triển Core
    Phase 3: Reindex, API & Auth           : p3, after p2, 7d
    Phase 4: Giao diện Web & Phân quyền UI : p4, after p3, 6d
    section Tích hợp & Đóng gói
    Phase 5: Kết nối Phần cứng (Local Agent) : p5, after p4, 5d
    Phase 6: Dockerize & Triển khai      : p6, after p5, 3d
    section Kiểm thử & Nghiệm thu
    Phase 7: Kiểm thử Toàn diện & Xác thực : p7, after p6, 4d
```

---

## 📑 MỤC LỤC TÀI LIỆU KỸ THUẬT CHI TIẾT THEO PHASE

Vui lòng nhấp vào các liên kết bên dưới để xem hướng dẫn thực hiện chi tiết cho từng Phase:

### 🛠️ [PHASE 1: Khởi Tạo Môi Trường & Monorepo](file:///d:/1_Project/48_Nexustock/phases/phase_1_setup.md)
* Thiết lập cấu trúc Monorepo (`backend/`, `frontend/`, `local-agent/`).
* Thiết lập file `docker-compose.yml` để chạy PostgreSQL và Redis phục vụ local development.
* Khởi tạo dự án ASP.NET Core API (chỉ dùng Npgsql) và Next.js App Router.

### 🗂️ [PHASE 2: Thiết Kế Database PostgreSQL, RBAC, Đối Tác & Các Phân Hệ Nâng Cao](file:///d:/1_Project/48_Nexustock/phases/phase_2_database_design.md)
* Thiết kế mô hình ERD độc lập chuẩn hóa 3NF trên PostgreSQL với 32 bảng quản lý nghiệp vụ Nhập - Xuất - QC - Vị trí kho - Đối tác - Kiểm kê định kỳ - Vùng kho bảo quản - Đợt gom hàng xuất (Wave Picking) - Hiệu suất lao động - Lịch hẹn bến bãi (Dock doors).
* Thiết lập cấu hình đặc tính sản phẩm (`weight_class`, `rotation_speed`) phục vụ thuật toán cất hàng tối ưu và cấu hình ngưỡng cảnh báo tồn tối thiểu/tối đa cho từng mã vật tư.
* Thiết lập hệ thống phân quyền chi tiết RBAC gồm các bảng: `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`.
* Xây dựng công cụ chuyển đổi dữ liệu lịch sử (Migration Script) từ hệ thống cũ sang PostgreSQL độc lập.

### ⚙️ [PHASE 3: Reindex Nghiệp Vụ Cũ, Viết API & Xác Thực JWT](file:///d:/1_Project/48_Nexustock/phases/phase_3_backend_reindex.md)
* Phân tích chuyên sâu (Reindex) logic các module cũ: Nhập kho, kiểm QC, chia nhỏ Lot, kiểm FIFO.
* Nhận diện và sửa các bug lịch sử (Bug rỗng cột Maker Lot trong màn hình chia nhỏ, cache trùng lặp Invoice).
* Xây dựng API Quản lý đối tác, API kiểm kê quét mã và tự động tạo phiếu điều chỉnh cân bằng kho, API cảnh báo thông minh (Lot sắp hết hạn, tồn kho vượt ngưỡng).
* Thiết lập thuật toán Slotting cất hàng tối ưu đề xuất 3 vị trí kệ trống khuyên dùng và luồng Cross-Docking tự động đề xuất chuyển tiếp trực tiếp khi khớp đơn hàng xuất gấp.
* Xây dựng API gom đơn hàng xuất kho (Wave Picking) tạo Pick List tối ưu hóa quãng đường di chuyển của nhân viên, API vẽ cây phả hệ Lot cha -> Lot con (Material Genealogy) phục vụ truy vết và API đo lường hiệu suất năng suất lao động (Labor Tracking).
* Xây dựng Middleware xác thực JWT Bearer và cơ chế kiểm soát quyền chi tiết `HasPermissionAttribute`.
* Cách ly dữ liệu Multi-tenant bằng EF Core Global Query Filter dựa trên `tenant_id`.

### 🎨 [PHASE 4: Phát Điển Giao Diện Web & Phân Quyền UI Client](file:///d:/1_Project/48_Nexustock/phases/phase_4_frontend_ui.md)
* Thiết lập hệ thống mã màu Dark Theme mặc định, bo góc mềm mại chuẩn Fluent Design / WinUI 3.
* Viết hook `useAuth` và component `HasPermission` để kiểm soát ẩn/hiện các nút bấm, biểu mẫu và các route điều hướng dựa trên danh sách Claim của tài khoản.
* Phát triển các thành phần UI: Form tiếp nhận quét mã hiển thị đề xuất Slotting và Cross-Docking, màn hình sơ đồ kho trực quan 2D/3D phân vùng nhiệt độ, màn hình đóng gói tích hợp WebSocket tự động lấy cân nặng từ cân điện tử và khóa nhập tay, màn hình quét kiểm kê định kỳ đối chiếu real-time, màn hình gom hàng xuất Wave Picking, màn hình sơ đồ cây gia phả Lot cha -> Lot con trực quan và các widget thông báo cảnh báo trên Dashboard.

### 🔌 [PHASE 5: Tích Hợp Thiết Bị Ngoại Vi (Local Agent)](file:///d:/1_Project/48_Nexustock/phases/phase_5_hardware_integration.md)
* Viết Windows Worker Service (C#) để đọc cổng COM ảo và giám sát thư mục file quét mã cầm tay.
* Tích hợp cổng Serial kết nối cân điện tử, đọc số cân thực tế và truyền thời gian thực qua WebSocket Server (`ws://localhost:9000`) đẩy lên trình duyệt Web.
* Giải pháp in thô ZPL/TSPL trực tiếp đến máy in nhãn mã vạch không qua Print Preview.

### 🐋 [PHASE 6: Đóng Gói Docker & Kịch Bản Triển Khai Production](file:///d:/1_Project/48_Nexustock/phases/phase_6_docker_deployment.md)
* Viết Multi-stage Build Dockerfile (nén SPA tĩnh vào thư mục `wwwroot` của Web API).
* Cấu hình định tuyến Fallback trên ASP.NET Core để hỗ trợ Client-side Routing.
* Kịch bản deploy bằng Docker Compose chạy duy nhất container ASP.NET Core kết nối PostgreSQL Production.

### 🧪 [PHASE 7: Kế Hoạch Kiểm Thử Toàn Diện & Xác Thực Hệ Thống](file:///d:/1_Project/48_Nexustock/phases/phase_7_testing_validation.md)
* Thiết lập tầng Unit Testing bằng xUnit cho Backend (kiểm FIFO, sinh Lot No, validate Regex) và Jest cho Frontend (phân quyền giao diện, chuyển đổi đơn vị).
* Thiết lập tầng Integration Testing kiểm thử kiểm soát truy cập API, concurrency lock chống race condition, thuật toán Slotting cất hàng, luồng Cross-Docking tự động, luồng phê duyệt chênh lệch kiểm kê (tự động tạo `StockAdjustment` và đồng bộ tồn kho), luồng gom đơn xuất Wave Picking, API phả hệ Lot (Genealogy Tree) và đo lường thời gian năng suất lao động (Labor Tracking).
* Thiết lập tầng End-to-End (E2E) Testing bằng Playwright giả lập hành vi người dùng quét nhận hàng gợi ý Slotting, kiểm QC, hold vật tư, quét xuất chặn FIFO, tự động lấy số cân đóng gói, thực thi trọn vẹn luồng kiểm kê chênh lệch, tạo đợt gom hàng xuất picking và tra cứu cây gia phả khoanh vùng sự cố hàng lỗi hàng loạt.
* Định nghĩa bảng tiêu chí nghiệm thu đầu ra (Acceptance Criteria) để đưa hệ thống vào vận hành sản xuất.
