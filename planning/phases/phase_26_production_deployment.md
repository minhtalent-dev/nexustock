# PHASE 26: DevOps & platform deployment

## Execution spec maturity

- **Mức hiện tại:** 95% execution-ready.
- **Đánh giá rp1:** Đã rà soát Phase 26 với roadmap tổng, trạng thái Phase 25 hoàn thành và cấu trúc project hiện tại. Plan đủ để bắt đầu triển khai DevOps/platform nếu FOUNDER approve.
- **Điểm đã nâng cấp:** Bổ sung migration rehearsal checklist, incident playbook, RTO/RPO, verify gates, secrets matrix, Docker Compose zero-downtime boundary và feature flag scope để tránh điểm mù khi triển khai.
- **Khi cần upgrade:** Nâng lên 100% sau khi Docker image build pass, backup/restore rehearsal pass, health checks pass và rollback drill có bằng chứng.

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
- Phase 25 Operational Observability đã hoàn thành để cung cấp dashboard, trace ID, timeline, KPI và alert phục vụ deployment monitoring.
- Quyền truy cập quản trị Server Production hoặc Staging (SSH, Docker privileges) đã được cấp cho DevOps.
- Domain, TLS certificate, reverse proxy entrypoint và firewall rule đã có owner rõ ràng.
- PostgreSQL production target đã có chính sách backup ngoài máy chủ hoặc volume an toàn.
- Docker image tag/version strategy đã thống nhất: dùng immutable tag theo version/build number, không deploy bằng `latest`.

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

### Boundary kiểm tra cấu trúc hiện tại

- Thư mục [docker](file:///d:/1_Project/48_Nexustock/docker) hiện chưa tồn tại trước Phase 26; đây là deliverable mới của phase.
- Thư mục [scripts](file:///d:/1_Project/48_Nexustock/scripts) hiện chưa tồn tại trước Phase 26; đây là deliverable mới của phase.
- File [docker-compose.yml](file:///d:/1_Project/48_Nexustock/docker-compose.yml) đang là cấu hình local/dev; Phase 26 phải tạo `docker/docker-compose.prod.yml` riêng, không phá cấu hình dev hiện có.
- Không đưa `.env.production` thật vào repo. Chỉ được commit `.env.production.example` không chứa secret thật.

### Cơ chế quản lý Migration Database:
- Khi chạy container Backend bản Production, migration được kích hoạt thông qua dòng lệnh khởi chạy (Entrypoint Script) gọi CLI: `dotnet EF Database Update` trước khi start process chính của ứng dụng Web.
- Nếu migration thất bại, tiến trình khởi động container bị hủy lập tức và container chuyển sang trạng thái `unhealthy` để chặn traffic.
- Production chỉ cho phép **một migration runner duy nhất** tại một thời điểm. Không để nhiều API replicas cùng chạy migration.
- Migration runner phải chạy sau pre-deploy backup và trước khi mở traffic.
- Nếu migration có thay đổi phá tương thích ngược, bắt buộc chia thành expand/contract migration hoặc chuyển sang Phase 30 hardening để diễn tập riêng.

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

### 8.1 Deployment flow chuẩn

1. Build image backend/frontend bằng immutable tag.
2. Chạy backend build gate và frontend lint/build gate trước khi push image.
3. Tạo backup pre-deploy và file checksum.
4. Chạy migration runner duy nhất.
5. Khởi động API/Web mới.
6. Kiểm tra `/health/live` và `/health/ready`.
7. Smoke test login, dashboard, webhook reliability và observability dashboard.
8. Mở traffic qua reverse proxy hoặc compose service update.
9. Theo dõi Phase 25 dashboard tối thiểu 15 phút: error rate, active alerts, DB connection, trace log.

### 8.2 Zero-downtime boundary

- Docker Compose đơn lẻ không bảo đảm zero-downtime tuyệt đối nếu không có reverse proxy/load balancer phía trước.
- Mục tiêu Phase 26: **near-zero downtime** cho single-server deployment, downtime kỳ vọng dưới 5 giây khi service restart nhanh.
- Zero-downtime đúng nghĩa cần ít nhất reverse proxy giữ old/new API song song hoặc orchestrator như Kubernetes/Swarm. Nếu production yêu cầu tuyệt đối 0 giây, nâng scope sang Phase 30.

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
- **Health check test:**
  - `/health/live` trả 200 khi process sống.
  - `/health/ready` trả 200 khi PostgreSQL/Redis sẵn sàng.
  - `/health/ready` trả 503 khi DB hoặc Redis bị ngắt.
  - Response không chứa connection string, password, token hoặc version chi tiết.
- **Secret scan thủ công:**
  - Kiểm tra không commit `.env.production` thật.
  - Kiểm tra `docker-compose.prod.yml` chỉ tham chiếu biến môi trường, không chứa mật khẩu thật.
- **Feature flag test:**
  - Env override có quyền ưu tiên cao nhất.
  - Tắt flag không cần redeploy image.
  - Flag hết lifecycle có checklist cleanup.

## 13. Acceptance criteria

- Quy trình build Docker thành công, không sinh lỗi biên dịch.
- Chạy cụm container bằng docker-compose lên sạch, `/health/live` trả về 200.
- `/health/ready` kiểm tra được PostgreSQL và Redis, trả 503 đúng khi dependency lỗi.
- File backup DB tạo ra đúng lịch, có checksum, khôi phục thành công trên môi trường diễn tập (Staging).
- Rollback tag và restore DB rehearsal hoàn tất dưới 2 phút trên staging.
- Tuyệt đối không có secret thật trong repo, image layer hoặc log triển khai.
- Không trộn logic deploy vào module nghiệp vụ WMS.
- Feature flag hoạt động cho ít nhất 5 phase core (P04, P06, P07, P13, P18): bật/tắt không cần rebuild image.
- Phase 25 Observability dashboard hiển thị hệ thống ổn định sau deploy smoke test.

## 14. Feature Flag & Progressive Rollout

Phase này thiết lập cơ chế kiểm soát tính năng theo từng giai đoạn để giảm rủi ro khi go-live. Đây là **infra flag** (không phải business data) — không áp dụng multi-tenant scope, không cần audit log per-row.

### 14.1 Feature Flag Architecture

Lưu trữ đơn giản nhất: DB table + env variable override (không cần thư viện bên ngoài).

```sql
CREATE TABLE "FeatureFlags" (
    "name"               VARCHAR(100) PRIMARY KEY,
    "enabled"            BOOLEAN NOT NULL DEFAULT FALSE,
    "rolloutPercentage"  INTEGER NOT NULL DEFAULT 0,  -- 0-100
    "whitelistUserIds"   TEXT,                         -- JSON array, nullable
    "description"        TEXT,
    "updatedAt"          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

**Ưu tiên đánh giá flag (thấp → cao):**
1. Default: `enabled = false`
2. DB row: `enabled` + `rolloutPercentage`
3. Env variable override: `FF_<NAME>=true/false` (dùng khi cần kill switch khẩn cấp không cần vào DB)

### 14.2 Flag Categories

| Category | Mục đích | Ví dụ | Lifecycle |
|---|---|---|---|
| `release` | Dark launch tính năng mới | `FF_ALLOCATION_V2` | Create → Rollout → Full → Cleanup |
| `ops` | Kill switch khẩn cấp | `FF_DISABLE_ERP_SYNC` | Create → Toggle → Cleanup nhanh |
| `experiment` | A/B test nhỏ nội bộ | `FF_NEW_SCAN_UI` | Create → Measure → Decision → Cleanup |

### 14.3 Progressive Rollout Flow

```
Bước 1: Internal (5%)
  └─ Chỉ bật cho Dev + FOUNDER account
  └─ Quan sát 24-48h: error rate, latency, user feedback

Bước 2: Power Users (25%)
  └─ Thêm whitelist: thủ kho trưởng + QC lead
  └─ Quan sát 3-5 ngày: nghiệp vụ edge case

Bước 3: One Warehouse (50%)
  └─ rolloutPercentage = 50 cho 1 kho pilot
  └─ Quan sát 1 tuần: load thực tế

Bước 4: Full Launch (100%)
  └─ rolloutPercentage = 100, xóa whitelistUserIds
  └─ Lên kế hoạch cleanup flag sau 2 sprint
```

### 14.4 Rollback via Flag

Tắt flag = instant rollback, **không cần redeploy, không cần migration DB**.

```bash
# Tắt khẩn cấp qua env (không cần vào DB):
export FF_ALLOCATION_V2=false
docker-compose restart api

# Hoặc update DB:
UPDATE "FeatureFlags" SET "enabled" = false WHERE "name" = 'FF_ALLOCATION_V2';
```

### 14.5 Flag Lifecycle — Cleanup Rule

Flag phải được xóa khỏi codebase và DB trong vòng **2 sprint** sau khi Full Launch để tránh technical debt. Dev chính chịu trách nhiệm track lifecycle trong issue tracker.

## 15. Secrets & Environment Matrix

| Nhóm biến | Bắt buộc | Nơi lưu | Ghi chú |
|---|:---:|---|---|
| `ConnectionStrings__DefaultConnection` | Có | Server env / Docker secret | Không commit vào repo |
| `Jwt__SigningKey` | Có | Server env / Docker secret | Tối thiểu 32 bytes entropy |
| `Redis__ConnectionString` | Có | Server env / Docker secret | Không trả trong health body |
| `Webhook__SigningSecret` | Có | Server env / Docker secret | Phục vụ Phase 24 HMAC |
| `Observability__EnableKpiSnapshotJob` | Có | Env | Có thể tắt khi rollback tải DB |
| `FF_*` | Tùy flag | Env ưu tiên cao nhất | Kill switch khẩn cấp |

## 16. Migration Rehearsal Checklist

### Pre-deploy

- [ ] Xác nhận current git commit/tag và image tag đang chạy.
- [ ] Tạo backup pre-deploy bằng `scripts/db-backup.sh`.
- [ ] Xác nhận file `.md5` hợp lệ.
- [ ] Chạy migration trên DB staging copy từ production backup.
- [ ] Chạy smoke test API và frontend trên staging.

### Deploy

- [ ] Pull image tag mới.
- [ ] Chạy migration runner duy nhất.
- [ ] Start API/Web containers.
- [ ] Kiểm tra `/health/live` và `/health/ready`.
- [ ] Kiểm tra login admin, dashboard, observability, webhook subscriptions.

### Post-deploy

- [ ] Theo dõi Phase 25 active alerts trong 15 phút.
- [ ] Kiểm tra container restart count = 0.
- [ ] Kiểm tra DB connection không tăng bất thường.
- [ ] Ghi nhận deployment evidence vào walkthrough/biên bản release.

## 17. Incident Playbook

| Incident | Tín hiệu | Hành động trong 5 phút đầu | Rollback |
|---|---|---|---|
| API không ready | `/health/ready` 503 | Kiểm tra DB/Redis env và logs container | Revert image tag trước |
| Migration fail | migration runner exit != 0 | Không mở traffic, giữ version cũ | Restore pre-deploy backup nếu DB đã đổi |
| DB connection spike | ready chập chờn, dashboard cảnh báo | Tắt job nặng bằng env, restart API | Revert image nếu không giảm |
| Frontend blank page | Web 200 nhưng UI lỗi | Kiểm tra static build/nginx route | Revert frontend image tag |
| Alert storm | Observability nhiều alert trùng | Tắt evaluator job bằng env | Giữ app read-only nếu cần |

## 18. RTO/RPO Targets

| Chỉ số | Mục tiêu Phase 26 | Điều kiện |
|---|---:|---|
| RTO rollback app | <= 2 phút | Image tag cũ còn local hoặc registry sẵn sàng |
| RTO restore DB staging | <= 15 phút | Backup size nhỏ/trung bình, cùng host |
| RPO backup daily | <= 24 giờ | Cron backup chạy hằng ngày |
| RPO pre-deploy | <= 5 phút | Backup ngay trước deploy |
| Downtime deploy single server | < 5 giây kỳ vọng | Không có migration khóa dài |

## 19. Verification Commands

```powershell
dotnet build backend/Nexustock.Api/Nexustock.Api.csproj --no-restore
npm run lint --prefix frontend -- --max-warnings 0
docker compose -f docker/docker-compose.prod.yml config
docker compose -f docker/docker-compose.prod.yml build
powershell -ExecutionPolicy Bypass -File tests/verify_production_health.ps1
powershell -ExecutionPolicy Bypass -File tests/verify_backup_restore.ps1
powershell -ExecutionPolicy Bypass -File tests/verify_deployment_rollback.ps1
git diff --check
```

## 20. rp1 Verdict

- **Kết luận:** Phase 26 sau cập nhật đã đủ chuẩn để chuyển sang bước lập plan triển khai chi tiết hoặc execution sau approval.
- **Không còn điểm mù lớn:** Docker structure, migration runner, backup/restore, health checks, rollback, secrets, feature flags và observability handoff đều đã có contract rõ.
- **Blocker trước execution:** Cần xác nhận target môi trường chạy test là local Docker Desktop hay staging server thật để chọn đường dẫn backup, port publish, domain và TLS mode.



