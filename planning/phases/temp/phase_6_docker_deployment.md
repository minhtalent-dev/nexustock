# PHASE 6: ĐÓNG GÓI DOCKER & KỊCH BẢN TRIỂN KHAI PRODUCTION (DEPLOYMENT)

Phase này hướng dẫn chi tiết cách viết Dockerfile tối ưu hóa kết hợp (Multi-stage Build), cấu hình Docker Compose và phân phối hệ thống **Nexustock** lên máy chủ vận hành của doanh nghiệp/nhà máy.

---

## 🐋 1. DOCKERFILE PHÂN GIAI ĐOẠN TÍCH HỢP (MULTI-STAGE DOCKERFILE)

Tạo tệp `Dockerfile` tại thư mục gốc của Monorepo. Dockerfile này sẽ xây dựng Next.js thành mã tĩnh, nhét vào thư mục `wwwroot` của ASP.NET Core và xuất ra một Image duy nhất siêu gọn nhẹ:

```dockerfile
# =======================================================
# STAGE 1: Biên dịch Frontend Next.js thành SPA Tĩnh
# =======================================================
FROM node:20-alpine AS frontend-builder
WORKDIR /src/frontend

# Copy package files và cài đặt dependencies
COPY frontend/package*.json ./
RUN npm ci

# Copy mã nguồn frontend và tiến hành build static export
COPY frontend/ ./
ENV NEXT_PUBLIC_API_URL="" 
# Để trống API URL để client tự động sử dụng đường dẫn tương đối (Relative Path)
RUN npm run build

# =======================================================
# STAGE 2: Biên dịch Backend ASP.NET Core Web API
# =======================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-builder
WORKDIR /src/backend

# Copy Solution và các file project để restore dependencies trước
COPY backend/*.sln ./
COPY backend/Nexustock.Api/*.csproj ./Nexustock.Api/
COPY backend/Nexustock.Core/*.csproj ./Nexustock.Core/
COPY backend/Nexustock.Data/*.csproj ./Nexustock.Data/
RUN dotnet restore

# Copy toàn bộ mã nguồn và biên dịch Release
COPY backend/ ./
RUN dotnet publish Nexustock.Api/Nexustock.Api.csproj -c Release -o /app/publish

# =======================================================
# STAGE 3: Runtime Container cuối cùng
# =======================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=backend-builder /app/publish .

# Copy kết quả build SPA tĩnh từ Stage 1 vào wwwroot của Web API
COPY --from=frontend-builder /src/frontend/out ./wwwroot

# Cấu hình biến môi trường chạy sản xuất
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80
ENTRYPOINT ["dotnet", "Nexustock.Api.dll"]
```

---

## ⚙️ 2. CẤU HÌNH ĐỊNH HƯỚNG SPA TRÊN ASP.NET CORE

Khi Next.js được xuất dưới dạng tĩnh (SPA), trình duyệt tự xử lý định tuyến (Routing). Nếu người dùng tải lại trang (F5) khi đang ở đường dẫn `/products/123`, IIS hoặc Kestrel sẽ trả về lỗi `404 Not Found` vì không tìm thấy file vật lý tương ứng.
Để giải quyết triệt để, chúng ta cấu hình Fallback Routing trong file `Program.cs` của ASP.NET Core:

```csharp
var app = builder.Build();

// Phục vụ các file tĩnh trong wwwroot (HTML, JS, CSS, logo...)
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllers(); // Định tuyến cho API Endpoints (/api/...)

// ⚠️ FALLBACK ROUTING: Toàn bộ request không phải API sẽ được trỏ về index.html của SPA
app.MapFallbackToFile("index.html");

app.Run();
```

---

## 🚀 3. KỊCH BẢN TRIỂN KHAI PRODUCTION (DEPLOYMENT COMPOSING)

Tạo file `docker-compose.prod.yml` để chạy ứng dụng kèm cơ sở dữ liệu trên Server chính thức của nhà máy:

```yaml
version: '3.8'

services:
  # 1. Ứng dụng tích hợp Nexustock
  nexustock-app:
    image: nexustock-app:latest
    build:
      context: .
      dockerfile: Dockerfile
    container_name: nexustock-app-prod
    ports:
      - "8080:80" # Chạy ứng dụng trên cổng 8080 của Server
    environment:
      - ConnectionStrings__NexustockDb=Host=prod-postgres-db;Database=nexustock_main;Username=prod_user;Password=prod_pass
    depends_on:
      - prod-postgres-db
    restart: always

  # 2. Database PostgreSQL nội bộ (Phục vụ kho chính)
  prod-postgres-db:
    image: postgres:16-alpine
    container_name: nexustock-postgres-prod
    environment:
      POSTGRES_DB: nexustock_main
      POSTGRES_USER: prod_user
      POSTGRES_PASSWORD: prod_pass
    volumes:
      - prod_postgres_data:/var/lib/postgresql/data
    restart: always

volumes:
  prod_postgres_data:
```

---

## 🛡️ 4. QUY TRÌNH ROLLBACK & KHÔI PHỤC KHẨN CẤP (ROLLBACK PROCEDURES)

* **Ghi nhận sự cố**: Nếu phát hiện bản cập nhật mới gây lỗi nghiêm trọng (Ví dụ: Treo hệ thống nhập kho, không in được nhãn).
* **Quy trình rollback**:
  1. Gắn tag version ổn định trước đó cho image (Ví dụ: `v1.2.0`).
  2. Cập nhật file compose trỏ về image cũ: `image: nexustock-app:v1.2.0`.
  3. Chạy lệnh deploy khẩn cấp:
     ```bash
     docker compose -f docker-compose.prod.yml up -d --force-recreate nexustock-app
     ```
  4. Hệ thống sẽ khôi phục về trạng thái ổn định cũ trong vòng dưới 10 giây.
