# PHASE 01: Project foundation

## Execution spec maturity

- **Mức hiện tại:** 98%
- **Đánh giá:** Đủ execution-ready cho monorepo skeleton, Modular Monolith backend, Docker local, Redis optional, Health Check UI, convention và README first-run.
- **Khi cần upgrade:** Chỉ cập nhật khi tech stack, port map hoặc repo convention đổi.

## 1. Mục tiêu

Thiết lập nền tảng dự án để đội phát triển có thể chạy, build và mở rộng Nexustock nhất quán.

Phase này thuộc stage **MVP vận hành chắc** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Monorepo, Docker local, cấu trúc backend/frontend/local-agent, chuẩn env, README vận hành local, health check API và Health Check UI nền.

### In scope

* Tạo cấu trúc backend, frontend, local-agent, planning, docs.
* Khởi tạo Backend ASP.NET Core 8 Web API theo Modular Monolith.
* Tạo sẵn 5 module skeleton: Identity, MasterData, Inbound, Inventory, Outbound.
* Khởi tạo Frontend Next.js App Router bằng TypeScript, Tailwind CSS, Shadcn UI và npm.
* Chuẩn hóa `.env.example` và `appsettings` template.
* Tạo Docker Compose local cho PostgreSQL và Redis optional.
* Thiết lập convention naming branch, migration, API route, permission.
* Tạo health endpoints `/health/live`, `/health/ready` và Health Check UI `/health-ui`.
* Viết README first-run và troubleshooting.

### Non-negotiable output

* Có database contract hoặc xác nhận không cần database.
* Có API contract hoặc xác nhận chỉ là cấu hình/tài liệu.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Repository Nexustock đã sẵn sàng và roadmap được duyệt.

### Readiness checklist

* Phase phụ thuộc đã pass acceptance criteria nếu có.
* Công cụ local đã sẵn sàng: Git, .NET 8 SDK, Node.js LTS, npm, Docker Desktop.
* Port local quan trọng chưa bị chiếm hoặc đã có port override qua env.
* Không có file secret thật trong thư mục dự án trước khi khởi tạo.

## 4. Setup

* Tạo cấu trúc monorepo: `backend/`, `frontend/`, `local-agent/`, `planning/`, `docs/`.
* Khởi tạo Backend ASP.NET Core 8 Web API và 5 module skeleton.
* Khởi tạo Frontend Next.js SPA.
* Chuẩn hóa `.env.example` và `appsettings` template.
* Tạo Docker Compose local chạy PostgreSQL và Redis optional.
* Thiết lập convention naming branch, migration, API route, permission.
* Viết README first-run và troubleshooting.

### Technology baseline

| Thành phần | Công nghệ / Quyết định | Ghi chú |
|---|---|---|
| Backend API | ASP.NET Core Web API (.NET 8) | Modular Monolith theo ADR 0001 |
| Frontend | Next.js App Router + TypeScript | SPA style cho web app quản trị |
| UI | Tailwind CSS + Shadcn UI | Không dùng inline style |
| Package manager | npm | Chuẩn mặc định cho frontend |
| Database | PostgreSQL | Database chính |
| Cache | Redis optional | Bật bằng `ENABLE_REDIS=true` |
| Local Agent | .NET 8 Worker Service | Phase 20 triển khai chi tiết |

### Local port map

| Service | Port mặc định | Env override |
|---|---:|---|
| Backend API | `5000` | `API_HTTP_PORT` |
| Frontend | `3000` | `FRONTEND_PORT` |
| PostgreSQL | `5435` | `POSTGRES_PORT` |
| Redis | `6379` | `REDIS_PORT` |

Nếu port bị chiếm, ưu tiên đổi qua `.env` thay vì sửa trực tiếp `docker-compose.yml`.

### Cấu trúc monorepo đề xuất

```text
backend/
  Nexustock.sln
  Nexustock.Api/
  modules/
    Nexustock.Modules.Identity/
    Nexustock.Modules.MasterData/
    Nexustock.Modules.Inbound/
    Nexustock.Modules.Inventory/
    Nexustock.Modules.Outbound/
frontend/
  src/
    app/
      health-ui/
    components/
    features/
local-agent/
planning/
docs/
```

### Backend bootstrap commands

```bash
dotnet new sln -n Nexustock

dotnet new webapi -n Nexustock.Api -o backend/Nexustock.Api

dotnet new classlib -n Nexustock.Modules.Identity -o backend/modules/Nexustock.Modules.Identity
dotnet new classlib -n Nexustock.Modules.MasterData -o backend/modules/Nexustock.Modules.MasterData
dotnet new classlib -n Nexustock.Modules.Inbound -o backend/modules/Nexustock.Modules.Inbound
dotnet new classlib -n Nexustock.Modules.Inventory -o backend/modules/Nexustock.Modules.Inventory
dotnet new classlib -n Nexustock.Modules.Outbound -o backend/modules/Nexustock.Modules.Outbound

dotnet sln Nexustock.sln add backend/Nexustock.Api/Nexustock.Api.csproj
dotnet sln Nexustock.sln add backend/modules/Nexustock.Modules.Identity/Nexustock.Modules.Identity.csproj
dotnet sln Nexustock.sln add backend/modules/Nexustock.Modules.MasterData/Nexustock.Modules.MasterData.csproj
dotnet sln Nexustock.sln add backend/modules/Nexustock.Modules.Inbound/Nexustock.Modules.Inbound.csproj
dotnet sln Nexustock.sln add backend/modules/Nexustock.Modules.Inventory/Nexustock.Modules.Inventory.csproj
dotnet sln Nexustock.sln add backend/modules/Nexustock.Modules.Outbound/Nexustock.Modules.Outbound.csproj

dotnet add backend/Nexustock.Api/Nexustock.Api.csproj reference backend/modules/Nexustock.Modules.Identity/Nexustock.Modules.Identity.csproj
dotnet add backend/Nexustock.Api/Nexustock.Api.csproj reference backend/modules/Nexustock.Modules.MasterData/Nexustock.Modules.MasterData.csproj
dotnet add backend/Nexustock.Api/Nexustock.Api.csproj reference backend/modules/Nexustock.Modules.Inbound/Nexustock.Modules.Inbound.csproj
dotnet add backend/Nexustock.Api/Nexustock.Api.csproj reference backend/modules/Nexustock.Modules.Inventory/Nexustock.Modules.Inventory.csproj
dotnet add backend/Nexustock.Api/Nexustock.Api.csproj reference backend/modules/Nexustock.Modules.Outbound/Nexustock.Modules.Outbound.csproj
```

### Frontend bootstrap commands

```bash
npx -y create-next-app@latest frontend --typescript --tailwind --app --src-dir --eslint --import-alias "@/*"
cd frontend
npm install lucide-react axios clsx tailwind-merge
npx shadcn-ui@latest init
```

### Baseline commands

| Mục tiêu | Command |
|---|---|
| Backend restore | `dotnet restore` |
| Backend build | `dotnet build` |
| Backend test | `dotnet test` |
| Frontend install | `npm install` |
| Frontend lint | `npm run lint` |
| Frontend run | `npm run dev` |
| Docker local | `docker compose up -d` |

### .gitignore template

```gitignore
# Secrets
.env
.env.*
!.env.example
appsettings.Production.json

# .NET
bin/
obj/
*.user
*.suo
.vs/
TestResults/

# Node / Next.js
node_modules/
.next/
out/
dist/
npm-debug.log*

# Docker local data
pgdata/
redis-data/

# OS / IDE
.DS_Store
Thumbs.db
.idea/
.vscode/
```

### Permission seed đề xuất

Phase 01 không seed permission nghiệp vụ.

Nếu cần route bảo vệ cho Health Check UI nội bộ sau này, dùng permission riêng ở Phase 03:

* system.health.read

Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `nexustock_main` | Database chính local | Encoding UTF-8, timezone UTC, migration quản lý bằng backend |
| `__EFMigrationsHistory` | Lịch sử migration EF Core | Không sửa tay ngoài migration tool |
| Redis database `0` | Cache optional | Chỉ dùng khi `ENABLE_REDIS=true` |

### Chuẩn database áp dụng

* Mọi bảng nghiệp vụ có `id`, `tenantId`, `createdAt`, `createdBy`, `updatedAt`, `updatedBy` nếu có chỉnh sửa.
* Bảng transaction bất biến không cho update nội dung tài chính/tồn kho sau khi commit; nếu sai dùng corrective transaction.
* Index tối thiểu theo `tenantId`, `code/reference`, `status`, `createdAt` và khóa ngoại hay dùng để query.
* Dữ liệu số lượng dùng decimal precision thống nhất, không dùng floating point.
* Status lưu bằng enum/string ổn định, không lưu text tự do.
* Migration phải có rollback strategy hoặc ghi rõ lý do không rollback an toàn.

### Transaction boundary

* Mọi thay đổi inventory hoặc trạng thái quan trọng phải nằm trong một transaction.
* Không gọi hệ thống ngoài trong DB transaction dài.
* Nếu cần publish event, dùng outbox/integration log sau commit.
* Chống double-submit bằng idempotency key ở command quan trọng.

## 6. Backend/API

| API | Mục đích | Ghi chú triển khai |
|---|---|---|
| `GET /health/live` | Kiểm tra tiến trình API đang sống | Không yêu cầu auth, không kiểm tra dependency ngoài |
| `GET /health/ready` | Kiểm tra readiness | Ping PostgreSQL; ping Redis chỉ khi `ENABLE_REDIS=true` |
| `GET /api/system/health-summary` | Trả dữ liệu cho `/health-ui` | Không trả connection string, secret hoặc cấu hình nhạy cảm |

### Health response contract

```json
{
  "status": "healthy",
  "version": "0.1.0",
  "environment": "Development",
  "services": {
    "api": "healthy",
    "database": "healthy",
    "redis": "disabled"
  },
  "traceId": "00-..."
}
```

### Redis startup rule

* Nếu `ENABLE_REDIS=false` hoặc thiếu env: không đăng ký Redis distributed cache, dùng in-memory cache dự phòng.
* Nếu `ENABLE_REDIS=true`: bắt buộc validate `REDIS_CONNECTION_STRING` khi startup.
* `/health/ready` chỉ fail vì Redis khi `ENABLE_REDIS=true` và Redis không kết nối được.
* Log trạng thái Redis không được ghi connection string.

### Quy chuẩn API

* Request/response dùng camelCase.
* Mutation API bắt buộc auth và permission.
* Response lỗi chuẩn gồm `errorCode`, `message`, `details`, `traceId`.
* Query API có pagination mặc định và max page size.
* Command API validate input tại boundary trước khi vào domain logic.
* Không trả dữ liệu tenant khác, kể cả khi biết id.

### Service layer

* Controller chỉ nhận request, validate model state, gọi application service.
* Application service điều phối transaction, permission, idempotency.
* Domain service xử lý rule nghiệp vụ thuần.
* Repository/query tách riêng command và read model khi query phức tạp.

### Modular Monolith boundaries

* `Nexustock.Api` là composition root, chịu trách nhiệm cấu hình DI, middleware, routing và health checks.
* Các module `Identity`, `MasterData`, `Inbound`, `Inventory`, `Outbound` là class library riêng.
* Module không truy cập trực tiếp internal class của module khác.
* Giao tiếp liên module ưu tiên qua interface công khai hoặc event nội bộ.
* Không join bảng xuyên module ở tầng repository nếu chưa có query model được phê duyệt.

## 7. Frontend/RF/mobile

| Màn hình/Control | Route | Mục đích | Yêu cầu UX |
|---|---|---|---|
| App shell | `/` | Khung giao diện quản trị | Dark theme, sidebar, topbar, route placeholder |
| Health Check UI | `/health-ui` | Kiểm tra trạng thái API/DB/Redis | Hiển thị trạng thái rõ ràng, tự refresh thủ công, không lộ secret |

### Health Check UI dashboard

`/health-ui` phải hiển thị tối thiểu 3 thẻ trạng thái:

| Thẻ | Trạng thái | Màu gợi ý | Nguồn dữ liệu |
|---|---|---|---|
| API Backend | Live / Offline | Green / Red | `GET /health/live` |
| Database | Connected / Disconnected | Green / Red | `GET /health/ready` |
| Redis Cache | Enabled / Disabled / Error | Green / Gray / Red | `GET /api/system/health-summary` |

### Chuẩn UI áp dụng

* UI text dùng Sentence case.
* Không dùng inline style.
* Sử dụng Next.js, Tailwind CSS và Shadcn UI. Không dùng inline style, tuân thủ component/style nhất quán.
* Mọi action nguy hiểm có confirm rõ ràng.
* Mọi màn hình có loading, empty, error, unauthorized state.
* Bảng dữ liệu có filter, pagination và trạng thái no result.
* RF/mobile ưu tiên input scan auto-focus, font lớn, ít nút, phản hồi rõ.

### State cần hiển thị

* Draft/open/in progress/completed/cancelled nếu phase có workflow.
* Locked/blocked/exception nếu thao tác bị chặn.
* Last updated và actor cho dữ liệu quan trọng.
* Trace ID hoặc reference ID khi cần hỗ trợ vận hành.

## 8. Execution flow

1. Clone repository.
2. Copy env template.
3. Kiểm tra port local theo Local port map.
4. Chạy Docker local.
5. Chạy backend.
6. Chạy frontend.
7. Mở `/health/live` và xác nhận API trả 200.
8. Mở `/health/ready` và xác nhận DB connected, Redis disabled/healthy theo env.
9. Mở `/health-ui` và xác nhận dashboard hiển thị trạng thái đúng.
10. Chạy baseline commands và cập nhật README nếu phát hiện lỗi setup.

### Flow guardrails

* Không bỏ qua bước validate env.
* Không commit secret thật.
* Không đưa nghiệp vụ kho vào Phase 01.
* Không tạo permission nghiệp vụ dư.
* Không hardcode port nếu đã có env override.

## 9. Validation & business rules

* Không commit secret.
* Không hardcode connection string.
* Không đưa business logic vào phase foundation.
* Mọi service đọc cấu hình từ env/appsettings.
* Redis phải chạy optional: tắt Redis không được làm API startup fail.
* Health endpoint không trả connection string, token, password hoặc thông tin nhạy cảm.
* Module skeleton chỉ chứa contract và registration rỗng; không chứa nghiệp vụ thật.

### Validation nền bắt buộc

* Validate tenant scope ở các phase có dữ liệu tenant.
* Validate status transition ở các phase có workflow.
* Validate permission theo action ở các phase có auth/RBAC.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

### First-run verification

| Kiểm tra | Kỳ vọng |
|---|---|
| `dotnet restore` | Pass |
| `dotnet build` | Pass |
| `dotnet test` | Pass hoặc không có test nhưng command chạy hợp lệ |
| `npm install` | Pass |
| `npm run lint` | Pass |
| `npm run dev` | Frontend chạy ở port `3000` |
| `docker compose up -d` | PostgreSQL chạy, Redis chạy nếu enabled |
| `GET /health/live` | HTTP 200 |
| `GET /health/ready` | HTTP 200 khi dependency sẵn sàng |
| `/health-ui` | Hiển thị trạng thái API/DB/Redis đúng |

## 10. Exception handling

* Port bị chiếm.
* Docker chưa chạy.
* DB chưa sẵn sàng.
* Redis disabled nhưng app vẫn cố connect.
* Redis enabled nhưng thiếu connection string.
* Frontend không gọi được API do env sai.
* Health UI hiển thị sai trạng thái do API response contract lệch.

### Mapping lỗi chuẩn

| Nhóm lỗi | Hành vi hệ thống |
|---|---|
| Input sai | Trả validation error, không ghi transaction |
| Thiếu quyền | Trả 403, ghi security audit nếu cần |
| Dữ liệu stale | Trả conflict, yêu cầu reload |
| Vi phạm rule kho | Block hoặc tạo operational exception theo severity |
| Lỗi thiết bị/tích hợp | Ghi integration/device log, cho retry hoặc fallback nếu an toàn |
| Lỗi không khôi phục | Ghi trace ID, rollback transaction, báo admin |

### Nguyên tắc exception

* Lỗi vận hành có thể xử lý nghiệp vụ thì tạo exception framework.
* Lỗi kỹ thuật chỉ tạo operational exception nếu ảnh hưởng tác vụ kho.
* Không nuốt lỗi âm thầm.
* Mọi override phải có reason và audit.

## 11. Observability

* Startup log có environment, version, trace root.
* Health endpoint log lỗi dependency.
* Health UI hiển thị trace ID khi lỗi để hỗ trợ đối soát log.
* Không log secret/env nhạy cảm.

### Log và trace

* Mỗi request có trace ID.
* Command quan trọng ghi audit log.
* Entity nghiệp vụ chính ghi activity timeline.
* Job nền và integration event truyền trace ID khi liên quan flow gốc.
* Log không chứa password, token, secret hoặc dữ liệu nhạy cảm không mask.

### KPI đề xuất

* Throughput theo ngày/ca/user nếu phase có thao tác vận hành.
* Aging của task mở hoặc exception mở.
* Tỷ lệ lỗi validation/rule block.
* Tỷ lệ retry/failure nếu phase có tích hợp.
* Độ chính xác tồn kho nếu phase ảnh hưởng inventory.

## 12. Test plan

* Backend `/health/live` trả 200.
* Backend `/health/ready` trả connected khi PostgreSQL chạy.
* Redis disabled không làm API fail startup.
* Redis enabled nhưng offline làm `/health/ready` báo unhealthy rõ ràng.
* Frontend render app shell.
* Frontend render `/health-ui` và hiển thị đúng API/DB/Redis status.
* README first-run chạy được trên máy sạch.

### Test matrix bắt buộc

| Nhóm test | Nội dung |
|---|---|
| Unit | Env parser, Redis enable/disable branch, health summary mapping |
| Integration | API + DB connection + health endpoints |
| E2E | `/health-ui` gọi API và hiển thị status |
| Negative | Port sai, DB offline, Redis enabled/offline, frontend env sai |
| Regression | Không phá structure planning và downstream phase docs |

### Dữ liệu test

* `.env.example` với Redis disabled.
* `.env.example` với Redis enabled.
* PostgreSQL container healthy.
* PostgreSQL container stopped.
* Redis container enabled/disabled.

## 13. Acceptance criteria

* Dev mới chạy local trong một lượt theo README.
* API, DB, Frontend hoạt động.
* Không có secret trong repo.
* `/health/live` và `/health/ready` hoạt động đúng theo NFR.
* `/health-ui` hiển thị trạng thái API/DB/Redis rõ ràng.
* 5 module skeleton backend được tạo sẵn và add vào solution.
* Redis optional: tắt Redis vẫn chạy backend bình thường.

### Definition of done

* Database migration chạy sạch trên database trống hoặc Phase 01 ghi rõ chưa cần migration nghiệp vụ.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* RBAC thật.
* Master data thật.
* Nghiệp vụ kho.
* CI/CD production.
* Local Agent device bridge.
* Redis caching nghiệp vụ.

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

* Không có phase phụ thuộc.
* Phụ thuộc công cụ local: .NET 8 SDK, Node.js LTS, npm, Docker Desktop, PostgreSQL image, Redis image optional.

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.
* Các phase sau dùng 5 module skeleton làm boundary mặc định.

## 16. Maintenance notes

* Mọi thay đổi cấu trúc project phải cập nhật README.
* Env mới phải thêm vào `.env.example`.
* Không đổi convention nếu chưa cập nhật toàn bộ phase sau.
* Nếu thêm module mới, phải add vào solution, cập nhật README và architecture notes.
* Nếu đổi health response contract, phải cập nhật `/health-ui` cùng lúc.

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

## 17. Extension points

* Thêm CI pipeline.
* Thêm container observability.
* Thêm seed data command.
* Thêm architecture boundary tests cho module dependency.
* Thêm `/health-ui` auto-refresh và uptime history.

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

## 18. Rollback notes

* Revert file cấu hình foundation.
* Xóa container/volume local nếu cần reset.
* Không ảnh hưởng dữ liệu production.
* Nếu module skeleton sai tên, sửa trước khi phase sau tham chiếu.
* Nếu `/health-ui` contract sai, sửa route/response trước khi triển khai monitoring.

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.
