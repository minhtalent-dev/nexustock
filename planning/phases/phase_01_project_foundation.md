# PHASE 01: Project foundation

## 1. Mục tiêu

Thiết lập nền tảng dự án để đội phát triển có thể chạy, build, kiểm thử và mở rộng Nexustock WMS nhất quán.

Phase này tạo baseline kỹ thuật cho toàn bộ phase 02-30. Không triển khai nghiệp vụ kho trong phase này.

## 2. Phạm vi

### In scope

* Tạo cấu trúc `backend`, `frontend`, `local-agent`, `planning`, `docs`.
* Chuẩn hóa `.env.example`, `appsettings` template và convention secret.
* Tạo Docker Compose local cho database và dependency tối thiểu.
* Tạo health endpoint chuẩn cho API, DB và dependency readiness.
* Thiết lập convention cho branch, migration, API route, permission, error envelope và trace ID.
* Viết README first-run, troubleshooting và reset local environment.
* Ghi nhận mô hình tenancy mặc định: multi-warehouse cùng tenant.

### Out of scope

* RBAC chi tiết.
* Master data nghiệp vụ.
* Inventory ledger.
* Receiving/outbound workflow.
* Local Agent device implementation.
* Production CI/CD.

## 3. Dependency

| Loại | Chi tiết |
|---|---|
| Upstream | Không có |
| Downstream trực tiếp | Phase 02, 03, 04, 20, 26 |
| Contract tạo ra | Project structure, env convention, health convention, error envelope, trace ID convention |
| Enterprise reference | [Phase dependency graph](../enterprise/phase_dependency_graph.md), [Security model](../enterprise/security_model.md), [Measurable acceptance criteria](../enterprise/measurable_acceptance_criteria.md) |

## 4. Architectural decisions

### Tenancy baseline

* Mặc định dùng mô hình multi-warehouse cùng tenant.
* `tenantId` đại diện công ty/tổ chức.
* `warehouseId` đại diện kho vận hành trong công ty.
* Foundation phải chuẩn bị convention để mọi phase sau giữ `tenantId` xuyên suốt.
* MVP không triển khai SaaS onboarding, billing hoặc self-service tenant provisioning.

### Repository structure

```text
backend/
frontend/
local-agent/
planning/
  enterprise/
  phases/
docs/
scripts/
```

### Naming convention

| Thành phần | Convention |
|---|---|
| API route | `/api/{domain}/{resource}` |
| Health route | `/health/live`, `/health/ready` |
| Permission | `{domain}.{action}` |
| Migration | `{yyyyMMddHHmm}_{short_description}` |
| Branch | `feature/phase-xx-short-name` |
| Error code | `{domain}.{reason}` |

## 5. Database

Phase này chỉ tạo database shell và migration baseline.

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `nexustock_main` | Database chính local | UTF-8, timezone UTC |
| `__MigrationHistory` | Lịch sử migration | Không sửa tay ngoài migration tool |

### Database rules

* Migration phải chạy sạch trên database trống.
* Không tạo bảng nghiệp vụ ở phase này.
* Không hardcode connection string.
* `.env.example` chỉ chứa key mẫu, không chứa secret thật.
* Timezone lưu UTC.

## 6. Backend/API

| API | Mục đích | Auth | Response |
|---|---|---|---|
| `GET /health/live` | Process liveness | Không | 200 nếu process sống |
| `GET /health/ready` | Dependency readiness | Không hoặc network-restricted | DB/dependency status đã mask |
| `GET /api/system/version` | Build/version info | Có thể public nội bộ | Version, environment, traceId |

### Error envelope chuẩn

```json
{
  "errorCode": "system.validationFailed",
  "message": "Request is invalid.",
  "details": {},
  "traceId": "trc_01hxyz"
}
```

### API rules

* Request/response dùng camelCase.
* Mọi response lỗi có `traceId`.
* Health endpoint không trả connection string, token, path secret hoặc raw exception nhạy cảm.
* Mutation API ở phase sau bắt buộc auth, permission và audit.

## 7. Frontend/RF/mobile

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| App shell | Khung giao diện quản trị | Dark theme, sidebar, topbar, route placeholder |
| Health page | Kiểm tra trạng thái dịch vụ | Hiển thị API/DB/frontend status rõ ràng |

### UI rules

* UI text dùng Sentence case.
* Không dùng inline style.
* Ưu tiên Bootstrap 5 nếu là web truyền thống.
* Tách CSS/JS riêng nếu không dùng SPA component style.
* Mọi interactive element có id/name rõ để test.

## 8. Execution flow

1. Clone repository.
2. Copy `.env.example` thành `.env` local.
3. Chạy Docker Compose local.
4. Chạy backend.
5. Chạy frontend.
6. Mở health page.
7. Xác nhận `/health/live`, `/health/ready` và frontend shell hoạt động.

## 9. Validation & business rules

* Không commit secret.
* Không hardcode connection string.
* Không đưa business logic kho vào phase foundation.
* Mọi service đọc cấu hình từ env/appsettings.
* Health readiness fail phải trả trạng thái rõ, không leak secret.
* Trace ID phải xuất hiện trong log request và error response.

## 10. Exception handling

| Lỗi | Hành vi mong muốn |
|---|---|
| Port bị chiếm | README có hướng dẫn đổi port hoặc dừng process |
| Docker chưa chạy | Health ready fail rõ dependency unavailable |
| DB chưa sẵn sàng | API vẫn live, ready fail |
| Frontend không gọi được API | Health page hiển thị lỗi cấu hình API base URL |
| Env thiếu key bắt buộc | Startup fail fast với message không chứa secret |

## 11. Observability

* Startup log có environment, version và trace root.
* Request log có method, path, status, elapsedMs, traceId.
* Health dependency failure log có dependency name, không có secret.
* Log không chứa password, token, connection string hoặc raw authorization header.

## 12. Test plan

| Nhóm test | Nội dung |
|---|---|
| Smoke | Backend start, frontend start, DB container start |
| API | `/health/live` trả 200, `/health/ready` trả dependency status |
| Config | Missing required env fail fast |
| Security | Secret scanner hoặc manual grep không thấy secret thật |
| Documentation | Dev mới chạy được theo README trên máy sạch |

## 13. Measurable acceptance criteria

* New developer chạy API, DB và frontend theo README trong một lượt, không cần hỏi thêm.
* `/health/live` trả 200 khi API process sống.
* `/health/ready` trả DB connected khi database sẵn sàng và không trả connection string.
* Frontend render app shell và health page.
* `.env.example` có đủ key bắt buộc nhưng không chứa secret thật.
* Error response mẫu có `errorCode`, `message`, `details`, `traceId` và dùng camelCase.
* Không có bảng nghiệp vụ bị tạo ở phase này.

## 14. Definition of done

* Database migration baseline chạy sạch trên database trống.
* README first-run và troubleshooting hoàn chỉnh.
* API health endpoint có test pass.
* Frontend health page thao tác được.
* Trace ID hoạt động trong log và error response.
* Không còn placeholder generic trong phần triển khai phase.

## 15. Maintenance notes

* Mọi env key mới phải cập nhật `.env.example` và README.
* Mọi đổi route health phải cập nhật phase 26 deployment.
* Mọi đổi error envelope phải cập nhật API contract core.
* Mọi đổi convention permission phải cập nhật phase 03.

## 16. Rollback notes

* Revert cấu trúc foundation nếu chưa có phase downstream phụ thuộc.
* Reset local Docker volume chỉ áp dụng môi trường dev.
* Không xóa migration đã chạy trên shared environment nếu chưa có rollback plan.
