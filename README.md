# Nexustock — WMS Modular Monolith

Dự án Hệ thống quản lý kho hàng (WMS) xây dựng theo kiến trúc Modular Monolith chuẩn sản xuất.

## 🚀 Công nghệ sử dụng
* **Backend**: .NET 8.0 (C# Web API, Entity Framework Core)
* **Database**: PostgreSQL (Operational Storage)
* **Caching**: Redis (Optional)
* **Frontend**: Next.js 16.2 (App Router, Tailwind CSS, Webpack)

## 📁 Cấu trúc Monorepo
* /backend — Trọng tâm dịch vụ Web API.
  * /backend/Nexustock.Api — Composition Root, DI, Middleware, Health Check.
  * /backend/modules — Các phân hệ lõi: Identity, MasterData, Inbound, Inventory, Outbound, RF/Mobile Scan, Exceptions, Rules, Putaway, Allocation, Replenishment, Lpn, Serial, Rma, Wave, MaterialGenealogy (Phả hệ vật tư - Phase 19), LocalAgent (Nền tảng kết nối thiết bị ngoại vi và WebSocket Local Agent - Phase 20), Webhook (Cơ chế gửi tin Webhook tin cậy và xử lý hàng đợi lỗi - Phase 24), Observability (Giám sát vận hành và cơ chế kiểm soát tính năng Feature Flags - Phase 25/26).
* /local-agent — Dịch vụ chạy ngầm cục bộ kết nối thiết bị ngoại vi (cân điện tử, máy in tem nhãn) trên Windows.
* /frontend — Giao diện quản trị Next.js.
  * /health-ui — Bảng điều khiển kiểm tra sức khỏe hệ thống.
* /docker — Các Dockerfile đóng gói và docker-compose môi trường production.
* /scripts — Các kịch bản sao lưu DB, phục hồi, và rollback dự phòng.
* /tests — Các kịch bản tự động kiểm thử hệ thống (Health Check, Backup/Restore, Rollback).
* /planning — Spec kỹ thuật và tài liệu nghiệp vụ từng Phase.


## 🛠️ Hướng dẫn khởi chạy nhanh (First-Run)

### 1. Khởi chạy Docker local (DB & Redis)
` ash
docker compose up -d
`

### 2. Thiết lập Environment
Sao chép cấu hình mẫu:
`ash
cp .env.example .env
`

### 3. Chạy Backend
`ash
cd backend/Nexustock.Api
dotnet run
`
API Host sẽ chạy tại cổng http://localhost:5024.

### 4. Chạy Frontend
` ash
cd frontend
npm run dev
`
Giao diện sẽ chạy tại cổng `http://localhost:3003`.

## 🩺 Endpoints kiểm tra sức khỏe
* **Liveness Probe**: GET http://localhost:5024/health/live (Trả về 200 OK nếu API Host sống)
* **Readiness Probe**: GET http://localhost:5024/health/ready (Kiểm tra kết nối DB và Redis)
* **Health Dashboard UI**: `http://localhost:3003/health-ui` (Giao diện giám sát thời gian thực)

## 🔒 Tài khoản quản trị mặc định
Sau khi khởi chạy Backend lần đầu, cơ sở dữ liệu sẽ tự động được migrate và seed tài khoản admin:
* **Email**: `admin@nexustock.com`
* **Mật khẩu**: `AdminSecret123!`
Tài khoản này được gán vai trò `Admin` và sở hữu đầy đủ quyền truy cập hệ thống.
