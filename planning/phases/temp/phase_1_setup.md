# PHASE 1: KHỞI TẠO CẤU TRÚC DỰ ÁN & THIẾT LẬP MÔI TRƯỜNG (INITIALIZATION)

Phase này thiết lập nền tảng mã nguồn (Monorepo), cài đặt các công cụ phát triển và thiết lập môi trường Docker chạy thử nghiệm local cho Nexustock sử dụng **PostgreSQL** làm cơ sở dữ liệu duy nhất.

---

## 📂 1. CẤU TRÚC THƯ MỤC DỰ ÁN (MONOREPO STRUCTURE)

Dự án được quy hoạch theo cấu trúc Monorepo để dễ dàng đồng bộ mã nguồn Backend và Frontend:
```text
nexustock/
├── backend/                   # ASP.NET Core Web API (C#)
│   ├── Nexustock.Api/         # Entry point, Controllers, Middleware
│   ├── Nexustock.Core/        # Business Logic, Interface Services, Domain Models
│   ├── Nexustock.Data/        # DB Context (EF Core), Migrations, Repositories
│   └── Nexustock.sln          # Solution File của Backend
├── frontend/                  # Next.js SPA (React / TypeScript)
│   ├── src/
│   │   ├── app/               # Next.js App Router (Pages, Layouts)
│   │   ├── components/        # UI Components (Fluent Design, Shadcn)
│   │   ├── hooks/             # Custom React Hooks (WebSocket, Auth)
│   │   ├── services/          # Client API Services (Axios / Fetch Client)
│   │   └── types/             # TypeScript Interfaces/Types
│   ├── next.config.js         # Cấu hình Next.js (bật output: 'export')
│   └── package.json           # Cấu hình npm packages
├── local-agent/               # Desktop Agent giao tiếp phần cứng (C# / Go)
│   ├── Nexustock.Agent/       # Worker Service lắng nghe COM port & WebSocket
│   └── Nexustock.Agent.sln    # Solution của Local Agent
├── docker-compose.yml         # Thiết lập container PostgreSQL và Redis
└── README.md                  # Hướng dẫn chạy dự án
```

---

## ⚙️ 2. THIẾT LẬP DOCKER COMPOSE CHO MÔI TRƯỜNG PHÁT TRIỂN
Tạo file `docker-compose.yml` tại thư mục gốc để giả lập toàn bộ cơ sở hạ tầng cơ sở dữ liệu:

```yaml
version: '3.8'

services:
  # 1. PostgreSQL: Cơ sở dữ liệu duy nhất cho toàn bộ hệ thống Nexustock
  postgres:
    image: postgres:16-alpine
    container_name: nexustock-postgres
    ports:
      - "5435:5435"
    environment:
      POSTGRES_DB: nexustock_main
      POSTGRES_USER: nexustock_user
      POSTGRES_PASSWORD: nexustock_secure_password
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: always

  # 2. Redis: Cache và quản lý Session
  redis:
    image: redis:7-alpine
    container_name: nexustock-redis
    ports:
      - "6379:6379"
    restart: always

volumes:
  postgres_data:
```

---

## 🛠️ 3. CÁCH THỨC TRIỂN KHAI CHI TIẾT (STEP-BY-STEP)

* **Bước 1: Khởi tạo Backend**
  1. Di chuyển vào thư mục `backend/`.
  2. Chạy lệnh:
     ```bash
     dotnet new sln -n Nexustock
     dotnet new webapi -n Nexustock.Api --use-controllers
     dotnet new classlib -n Nexustock.Core
     dotnet new classlib -n Nexustock.Data
     ```
  3. Thêm tham chiếu (Reference) giữa các project:
     * `Nexustock.Api` tham chiếu đến `Nexustock.Core` và `Nexustock.Data`.
     * `Nexustock.Data` tham chiếu đến `Nexustock.Core`.
  4. Thêm các thư viện NuGet cần thiết vào `Nexustock.Data`:
     ```bash
     dotnet add Nexustock.Data package Npgsql.EntityFrameworkCore.PostgreSQL
     dotnet add Nexustock.Data package Microsoft.EntityFrameworkCore.Tools
     ```
     *(Lưu ý: Không cài đặt Microsoft.EntityFrameworkCore.SqlServer để đảm bảo backend sạch bóng công nghệ cũ)*

* **Bước 2: Khởi tạo Frontend**
  1. Di chuyển vào thư mục `frontend/`.
  2. Khởi chạy CLI thiết lập dự án:
     ```bash
     npx -y create-next-app@latest ./ --typescript --tailwind --app --src-dir --eslint --import-alias "@/*"
     ```
  3. Cài đặt các thư viện bổ sung cho giao diện:
     ```bash
     npm install lucide-react axios clsx tailwind-merge
     ```

* **Bước 3: Khởi động môi trường Database**
  1. Chạy lệnh `docker-compose up -d` để khởi động PostgreSQL và Redis.
  2. Sử dụng công cụ quản trị (DBeaver) kết nối kiểm tra trạng thái hoạt động của database PostgreSQL.
