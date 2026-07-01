# PHASE 26: DevOps & platform deployment

## 1. Mục tiêu

Thiết lập hạ tầng đóng gói, triển khai và vận hành hệ thống Nexustock WMS chuẩn Enterprise. Chuyển đổi định hướng phase này hoàn toàn sang **DevOps & Platform Workstream** (không xây dựng module code CRUD nghiệp vụ cho deployment). Đảm bảo quy trình build Docker, migration database tự động, health checks an toàn, sao lưu tự động và kịch bản rollback khẩn cấp hoạt động trơn tru.

## 2. Phạm vi

### In scope

- Viết các tệp tin đóng gói `Dockerfile` tối ưu hóa nhiều tầng (Multi-stage build) cho Backend (ASP.NET Core) và Frontend (Single Page App - Nginx).
- Xây dựng cấu hình `docker-compose.prod.yml` chạy cụm dịch vụ: API Backend, Web SPA, PostgreSQL Database, và Redis.
- Triển khai endpoints kiểm tra sức khỏe hệ thống không yêu cầu auth nhưng bảo vệ bằng Network ACLs: `/health/live` và `/health/ready`.
- Viết kịch bản sao lưu tự động (Automated Backup Script) cho PostgreSQL bằng `pg_dump` kết hợp nén và kiểm tra MD5 checksum.
- Thiết lập quy trình chạy Database Migration tự động khi khởi chạy container hoặc qua CLI.
- Định nghĩa quy trình triển khai không gián đoạn (Zero-downtime Rolling Update) và kịch bản Rollback phiên bản tức thời.

### Non-negotiable output

- Docker images của Backend và Frontend được build thành công, dung lượng tối giản, không chứa công cụ build thừa (development SDK).
- File `.env.production` tách biệt hoàn toàn khỏi repo, quản lý bí mật qua biến môi trường của hệ thống máy chủ (Environment Variables).
- Scripts backup hoạt động độc lập, tự động chạy qua Cron/Task Scheduler cục bộ.
- Kịch bản Rollback bằng cách trỏ Docker compose image tag về phiên bản cũ và chạy script rollback database.

## 3. Điều kiện đầu vào

### Readiness checklist

- Toàn bộ 25 phase nghiệp vụ WMS và tích hợp đã vượt qua kiểm thử local và tích hợp.
- Quyền truy cập quản trị Server Production (SSH, Docker privileges) đã được cấp cho DevOps.

## 4. Setup

### Cấu trúc thư mục Platform đề xuất

Tất cả các tài liệu cấu hình, script triển khai được tổ chức độc lập trong thư mục gốc của dự án:
```text
docker/
  ├── backend/
  │     └── Dockerfile
  ├── frontend/
  │     ├── Dockerfile
  │     └── nginx.conf
  └── docker-compose.prod.yml
scripts/
  ├── db-backup.sh
  ├── db-restore.sh
  └── deploy-rollback.sh
```

## 5. Database & Infrastructure

*Không tạo các bảng `DeploymentRecords` hay `BackupRecords` trong DB nghiệp vụ kho của WMS để tránh trộn lẫn trách nhiệm hệ thống.*

### Cơ chế quản lý Migration Database:
- Khi chạy container Backend bản Production, migration được kích hoạt thông qua dòng lệnh khởi chạy (Entrypoint Script) gọi CLI: `dotnet EF Database Update` trước khi start process chính của ứng dụng Web.
- Nếu migration thất bại, tiến trình khởi động container bị hủy lập tức và container chuyển sang trạng thái `unhealthy` để chặn traffic.

### Cơ chế Sao lưu Database (`db-backup.sh`):
- Định kỳ chạy job cron (ví dụ: 01:00 AM hàng ngày) thực thi lệnh:
  ```bash
  pg_dump -h db -U postgres -d nexustock_main | gzip > /var/backups/nexustock/db_backup_$(date +%Y%m%d_%H%M%S).sql.gz
  ```
- Kiểm tra tính toàn vẹn của tệp sao lưu bằng cách tạo file `.md5` đi kèm.
- Giữ lại tối đa 30 bản backup gần nhất, tự động xóa các bản cũ hơn.

## 6. Backend/API Health Check Rules

Hệ thống cung cấp 2 Endpoint Health Check riêng biệt, được cấu hình tại Middleware của ASP.NET Core:

### 6.1 Endpoint Liveness (`GET /health/live`)
- **Mục đích:** Để container orchestrator (như Docker Compose hoặc Kubernetes) biết tiến trình (Process) API của Nexustock có đang sống không.
- **Quy tắc bảo mật:** Không yêu cầu xác thực JWT/API Key (tránh lỗi ngắt traffic do hết hạn token).
- **Hành vi:** Chỉ trả về trạng thái HTTP `200 OK` kèm JSON: `{ "status": "Healthy" }` nếu ứng dụng khởi chạy bình thường.

### 6.2 Endpoint Readiness (`GET /health/ready`)
- **Mục đích:** Kiểm tra xem API đã sẵn sàng nhận request từ người dùng chưa (các kết nối ngoại vi có thông suốt không).
- **Quy tắc bảo mật:** Không yêu cầu auth nghiệp vụ. Chỉ cho phép các địa chỉ IP nội bộ (Internal IP/Reverse Proxy) gọi đến.
- **Hành vi:**
  - Thực hiện ping truy vấn thử đến PostgreSQL (`SELECT 1`) và Redis.
  - Nếu tất cả kết nối phản hồi tốt: Trả về HTTP `200 OK` kèm trạng thái chi tiết.
  - Nếu có bất kỳ kết nối nào lỗi (ví dụ: DB bị nghẽn kết nối): Trả về HTTP `503 Service Unavailable`.
  - **TUYỆT ĐỐI CẤM** trả về thông tin nhạy cảm như connection string, password, phiên bản phần mềm chi tiết trong response body.

## 7. Frontend/Nginx Configuration

- File `frontend/Dockerfile` sử dụng image `nginx:alpine` siêu nhẹ.
- Cấu hình Nginx reverse proxy chuyển tiếp các request bắt đầu bằng `/api/` sang container backend API.
- Cấu hình bảo mật HTTP Headers (X-Frame-Options, X-Content-Type-Options, Content-Security-Policy).

## 8. Execution flow

### Kịch bản Triển khai và Rollback Khẩn cấp (Rollback Runbook Flow)

Khi phát hiện phiên bản mới bị lỗi nghiêm trọng trên production:

1. **Bước 1: Ngắt traffic lỗi**
   - DevOps đăng nhập server, đổi tag image trong tệp `docker-compose.prod.yml` về tag phiên bản ổn định trước đó (ví dụ: `v1.2.4` về `v1.2.3`).
2. **Bước 2: Khôi phục DB nếu có thay đổi schema không tương thích**
   - Chạy script khôi phục database từ bản backup tạo tự động ngay trước thời điểm deploy:
     ```bash
     bash scripts/db-restore.sh /var/backups/nexustock/db_backup_pre_deploy_v1.2.4.sql.gz
     ```
3. **Bước 3: Khởi động lại dịch vụ**
   - Chạy lệnh: `docker-compose down && docker-compose up -d`
4. **Bước 4: Kiểm tra khôi phục**
   - Gọi API `/health/ready` để xác minh hệ thống đã trở lại trạng thái sẵn sàng.

## 9. Validation & Platform rules

- **Không lưu trữ Secret trong mã nguồn:** Tất cả các token, mật khẩu DB, khóa Webhook Secret, JWT Signing Key phải được nạp thông qua biến môi trường (Environment Variables) hoặc Docker Secrets.
- **Quy tắc chạy Migration:** Tuyệt đối không cho phép tự động chạy Migration tự phát trong quá trình chạy ứng dụng chính (auto-migration on-demand) để tránh gây khóa bảng (DB Lock) diện rộng khi có nhiều node ứng dụng cùng khởi động. Migration chỉ chạy duy nhất 1 lần ở bước Init container.

## 10. Exception handling

- **Lỗi hết ổ đĩa (Disk Space Out):** Script backup kiểm tra dung lượng trống của ổ đĩa trước khi dump DB. Nếu dung lượng dưới 10%, dừng backup và phát cảnh báo Slack/Telegram khẩn cấp.
- **Lỗi Migration Conflict:** Nếu chạy migration mới bị lỗi xung đột với dữ liệu hiện tại, CLI migration trả mã thoát lỗi (Exit Code != 0). Container Init dừng build và thông báo lỗi log chi tiết.

## 11. Observability

- Logs từ các container Docker được thu gom tập trung bằng Docker Logging Driver (JSON-file hoặc chuyển tiếp sang Vector/Loki).
- KPI: Tỷ lệ CPU/RAM của server, số lượng kết nối DB đang hoạt động, thời gian chết khi triển khai (dưới 5 giây).

## 12. Test plan

- **Diễn tập sao lưu & Khôi phục (Backup & Restore Rehearsal):**
  - Chạy script backup để xuất file. Dựng một instance container DB phụ và chạy script restore. Kiểm tra dữ liệu sau restore trùng khớp 100% về checksum và số lượng dòng.
- **Diễn tập Rollback:**
  - Triển khai mock update bị lỗi, thực thi quy trình rollback tag và verify hệ thống trở lại bình thường dưới 2 phút.

## 13. Acceptance criteria

- Quy trình build Docker thành công, không sinh lỗi biên dịch.
- Chạy cụm container bằng docker-compose lên sạch, `/health/live` trả về 200.
- File backup DB tạo ra đúng lịch, khôi phục thành công trên môi trường diễn tập (Staging).
- Tuyệt đối không có bất kỳ file backend/frontend nghiệp vụ nào có logic code liên quan đến tác vụ deploy (Phase deployment sạch 100% code nghiệp vụ).
