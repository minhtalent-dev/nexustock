# Release Runbook & Governance - Nexustock WMS

Tài liệu đặc tả quy trình đóng gói, CI/CD, phát hành (Release) và cắt chuyển dữ liệu (Cutover) sang môi trường Production.

---

## 1. Kiến trúc Đóng gói & Deploy Topology

Nexustock chạy trên môi trường Production sử dụng kiến trúc Container hóa.

### 1.1 Sơ đồ Topology
```text
                  +-----------------------------------+
                  |           Internet/WAN            |
                  +-----------------+-----------------+
                                    | HTTPS (SSL 1.3)
                                    v
                  +-----------------+-----------------+
                  |       Nginx Reverse Proxy         |
                  +--------+-----------------+--------+
                           |                 |
            HTTP /api      v                 v Static SPA
              +------------+----+       +----+------------+
              | ASP.NET Core VM |       | Next.js SPA VM  |
              +--------+--------+       +-----------------+
                       |
        +--------------+--------------+
        |                             |
        v                             v
  +-----+------+                +-----+------+
  | PostgreSQL |                | Redis Cache|
  +------------+                +------------+
```

### 1.2 File cấu trúc docker-compose.prod.yml mẫu
```yaml
version: '3.8'

services:
  nginx:
    image: nginx:alpine
    ports:
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - /etc/letsencrypt:/etc/letsencrypt:ro
    depends_on:
      - wms-api
      - wms-web

  wms-api:
    image: nexustock/wms-api:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=nexustock_prod;Username=postgres;Password=${DB_PASSWORD}
      - Redis__Configuration=redis:6379
    depends_on:
      - db
      - redis

  wms-web:
    image: nexustock/wms-web:latest
    environment:
      - NEXT_PUBLIC_API_URL=https://api.nexustock.vn

  db:
    image: postgres:15-alpine
    volumes:
      - pgdata:/var/lib/postgresql/data
    environment:
      - POSTGRES_DB=nexustock_prod
      - POSTGRES_PASSWORD=${DB_PASSWORD}

  redis:
    image: redis:7-alpine

volumes:
  pgdata:
```

---

## 2. GitHub Actions CI/CD Pipeline Spec

Quy trình tự động hóa kiểm thử và đóng gói Docker Image khi có pull request/merge vào branch `main`.

```yaml
name: Production Release CI/CD

on:
  push:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3

    # 1. Setup & Test Backend (.NET)
    - name: Setup .NET SDK
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    - name: Restore dependencies
      run: dotnet restore backend/Nexustock.sln
    - name: Run Backend Unit & Integration Tests
      run: dotnet test backend/Nexustock.sln --no-restore --configuration Release

    # 2. Setup & Test Frontend (NodeJS)
    - name: Setup NodeJS
      uses: actions/setup-node@v3
      with:
        node-version: '18'
    - name: Install JS dependencies
      run: npm ci --prefix frontend
    - name: Run Frontend Tests
      run: npm test --prefix frontend

    # 3. Build & Push Docker Images
    - name: Log in to Docker Hub
      uses: docker/login-action@v2
      with:
        username: ${{ secrets.DOCKER_USERNAME }}
        password: ${{ secrets.DOCKER_PASSWORD }}

    - name: Build and Push Backend Image
      uses: docker/build-push-action@v4
      with:
        context: ./backend
        file: ./backend/Dockerfile
        push: true
        tags: nexustock/wms-api:latest,nexustock/wms-api:${{ github.sha }}

    - name: Build and Push Frontend Image
      uses: docker/build-push-action@v4
      with:
        context: ./frontend
        file: ./frontend/Dockerfile
        push: true
        tags: nexustock/wms-web:latest,nexustock/wms-web:${{ github.sha }}
```

---

## 3. Quy trình Cắt chuyển dữ liệu (Cutover) & Diễn tập Rollback
Chi tiết các bước rollback khẩn cấp và Hypercare support được chốt tại tài liệu tổng [release_runbook_governance.md](file:///d:/1_Project/48_Nexustock/planning/enterprise/release_runbook_governance.md).
- **RTO (Recovery Time Objective):** Dưới **2 giờ** khôi phục database từ file dump và restart containers.
- **RPO (Recovery Point Objective):** Dưới **1 giờ** lệch dữ liệu (nhờ sao lưu DB ngay trước thời điểm cutover).
- **Data Reconciliation:** Sau khi rollback, Ops Admin quét đối chiếu log Outbox và API logs để xác định các transactions bị mất và yêu cầu ERP gửi bù (Replay) qua `Idempotency-Key` định danh duy nhất.
