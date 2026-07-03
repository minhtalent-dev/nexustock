# Lịch sử cập nhật dự án

## Phiên bản 0.2.0 - Quản lý danh mục nền tảng (Phase 2)
*Ngày cập nhật: 02/07/2026*

### Tính năng mới
- **Quản lý danh mục vật tư**: Cho phép xem danh sách, thêm mới, cập nhật thông tin chi tiết của các loại vật tư và đơn vị tính (UOM).
- **Import dữ liệu 2 bước**: Hỗ trợ tải dữ liệu hàng loạt từ file mẫu. Hệ thống tự động kiểm tra và chỉ ra các lỗi dữ liệu trực quan trước khi xác nhận lưu chính thức.
- **Tải tệp mẫu và tệp lỗi**: Cho phép tải tệp dữ liệu mẫu để nhập liệu dễ dàng. Khi có lỗi import, hệ thống xuất tệp lỗi chỉ rõ nguyên nhân ở từng dòng.
- **Tài liệu hướng dẫn tích hợp**: Hệ thống API được chuẩn hóa và tài liệu hóa trực quan giúp kết nối dễ dàng.
- **Thông báo hệ thống trực quan**: Tích hợp thư viện Sonner toast thông minh giúp hiển thị thông báo thành công, cảnh báo, lỗi đẹp mắt và đồng bộ.

### Cải tiến và tối ưu
- **Xác nhận thao tác đồng bộ**: Thay hộp xác nhận mặc định của trình duyệt bằng dialog hiện đại, rõ ràng và đồng nhất với giao diện hệ thống.
- **Khắc phục lỗi form Thêm mới**: Sửa lỗi logic điều khiển dialog giúp nút "Thêm mới" hoạt động mượt mà trên tất cả các trang Master Data.
- **Độ tin cậy dữ liệu**: Đảm bảo toàn bộ dữ liệu import được lưu trữ đồng bộ, không xảy ra tình trạng lưu trữ nửa chừng khi có lỗi phát sinh.
- **Tăng tốc độ tìm kiếm**: Cải thiện hiệu năng tải danh sách danh mục vật tư và kho bãi.