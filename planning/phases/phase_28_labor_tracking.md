# PHASE 28: Labor tracking

## Execution spec maturity

- **Mức hiện tại:** 100% execution-ready.
- **Đánh giá:** Đủ contract rõ để executor triển khai không suy đoán. Đã khóa các blind spots về nguồn task, ca làm việc, zone, trạng thái timer, KPI và tương thích RF/mobile hiện có.
- **rp1 verdict:** Nâng từ 88% → 100% sau khi bổ sung module convention, DB schema chi tiết, API contract, status transition, KPI formula, timeout policy, feature flag, permission scope và test matrix nghiêm ngặt.

## rp1 — Blind-spot closure matrix

| Blind spot | Closure |
|---|---|
| Module path `backend/modules/labor_tracking` sai convention | Dùng `backend/modules/Nexustock.Modules.LaborTracking` và namespace `Nexustock.Modules.LaborTracking` để khớp toàn project. |
| Nguồn task chưa rõ | Phase 28 không thay thế `MobileTask`, `PickTask`, `WavePickTask`; chỉ tạo labor session tham chiếu bằng `sourceTaskType` + `sourceTaskId`. |
| `MobileTask` hiện chỉ có `Open/In_Progress/Completed` và thiếu timestamp start/complete | Labor module tự lưu `startedAt`, `pausedAt`, `resumedAt`, `completedAt`, `durationSeconds`; không sửa schema `MobileTasks` trong Phase 28. |
| Ca làm việc chưa có master data | Tạo bảng `LaborShifts` riêng trong module LaborTracking. Nếu không tìm thấy shift đang mở, API start tự tạo shift ngày hiện tại cho user. |
| Zone lấy từ đâu chưa rõ | Derive `zoneId` từ `StorageLocation.ZoneId` qua `locationId` của source task; nếu source task không có location thì lưu `zoneId = null` và KPI zone bỏ qua bản ghi này. |
| Một user chạy nhiều task song song chưa khóa | Mặc định chặn song song: mỗi user chỉ có tối đa 1 `Running` labor session / tenant. Override không thuộc Phase 28. |
| Pause/resume có cần không nhưng API cũ không có | Phase 28 triển khai pause/resume trong labor session riêng; không đổi endpoint RF/mobile cũ. |
| KPI formula mơ hồ | Chuẩn hóa: throughput = `completedTaskCount`, active time = tổng `durationSeconds - pausedSeconds`, idle time = shift elapsed - active time, avg seconds/task = active time / completedTaskCount. |
| Permission dư `approve/export` | Chỉ seed `labor_tracking.read`, `labor_tracking.create`, `labor_tracking.update`, `labor_tracking.export` nếu có export UI. Không seed `approve` vì không có approval flow. |
| Feature flag chưa định danh | Flag cố định: `FF_LABOR_TRACKING_ENABLED`; mọi endpoint `/api/labor/*` phải gate flag. |
| Phase 29 Task interleaving dependency | Phase 29 chỉ được đọc `LaborSessions` và KPI; không được thay đổi status hoặc duration của Phase 28. |

## 1. Mục tiêu

Đo năng suất theo task, user, ca, zone và loại thao tác.

Phase này thuộc stage **Optimization & automation** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Đo năng suất theo task, user, ca, zone và loại thao tác.

### In scope

* Tạo module Labor tracking
* Bật feature flag/permission
* Chuẩn hóa KPI

### Non-negotiable output

* Có database contract hoặc xác nhận không cần database.
* Có API contract hoặc xác nhận chỉ là cấu hình/tài liệu.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Stage trước đã ổn định và có dữ liệu vận hành thực tế.

### Readiness checklist

* Phase phụ thuộc đã pass acceptance criteria.
* Master data tối thiểu đã có nếu phase cần dữ liệu vận hành.
* Permission liên quan đã được seed hoặc có kế hoạch seed.
* Không còn migration pending từ phase trước.
* Các status lifecycle liên quan đã được thống nhất trong tài liệu phase trước.

## 4. Setup

* Tạo module Labor tracking
* Bật feature flag/permission
* Chuẩn hóa KPI

### Cấu trúc module đề xuất

```text
backend/modules/Nexustock.Modules.LaborTracking/
  Nexustock.Modules.LaborTracking.csproj
  DependencyInjection.cs
  Contexts/LaborTrackingDbContext.cs
  Contexts/LaborTrackingDbContextFactory.cs
  Entities/LaborSession.cs
  Entities/LaborSessionEvent.cs
  Entities/LaborShift.cs
  Services/ILaborTrackingService.cs
  Services/LaborTrackingService.cs
  Controllers/LaborController.cs
  DTOs/LaborDtos.cs
  Migrations/
frontend/src/app/admin/labor/
  page.tsx                  (labor KPI dashboard)
  sessions/page.tsx         (labor sessions list)
frontend/src/lib/api/labor.ts
tests/verify_labor_tracking.ps1
planning/phases/phase_28_labor_tracking.md
```

**Dependencies `.csproj`:**
- `Nexustock.Modules.Inventory` (`MobileTask`, `PickTask` lookup)
- `Nexustock.Modules.Wave` (`WavePickTask` lookup)
- `Nexustock.Modules.MasterData` (`StorageLocation.ZoneId` lookup)
- `Nexustock.Modules.Identity` (user/permission context)
- `Nexustock.Modules.Observability` (`IFeatureFlagService`, `IActivityTimelineService`, trace context)
- `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11`

### Permission seed đề xuất

* labor_tracking.read
* labor_tracking.create
* labor_tracking.update
* labor_tracking.approve
* labor_tracking.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

### Schema chi tiết

**`"LaborSessions"`**

| Column | Type | Constraint | Ghi chú |
|---|---|---|---|
| `id` | `uuid` | PK | `gen_random_uuid()` |
| `tenantId` | `uuid` | NOT NULL | Multi-tenant scope |
| `sourceTaskType` | `varchar(50)` | NOT NULL | `MobileTask`, `PickTask`, `WavePickTask`, `Manual` |
| `sourceTaskId` | `uuid` | nullable | ID task nguồn nếu có |
| `referenceType` | `varchar(80)` | NOT NULL | Copy từ task nguồn hoặc nhập tay |
| `referenceId` | `uuid` | nullable | Business reference |
| `userId` | `varchar(200)` | NOT NULL | Username/login hiện có, không cần FK cứng |
| `shiftId` | `uuid` | NOT NULL | FK mềm đến `LaborShifts.id` |
| `locationId` | `uuid` | nullable | Từ source task |
| `zoneId` | `uuid` | nullable | Derive từ `StorageLocation.ZoneId` |
| `operationType` | `varchar(50)` | NOT NULL | `Picking`, `Putaway`, `Replenishment`, `Movement`, `Packing`, `Count`, `Manual` |
| `status` | `varchar(30)` | NOT NULL | `Running`, `Paused`, `Completed`, `Cancelled`, `TimedOut` |
| `startedAt` | `timestamptz` | NOT NULL | Start timer |
| `completedAt` | `timestamptz` | nullable | End timer |
| `durationSeconds` | `integer` | NOT NULL DEFAULT 0 | Active elapsed, exclude pause |
| `pausedSeconds` | `integer` | NOT NULL DEFAULT 0 | Tổng thời gian pause |
| `lastPausedAt` | `timestamptz` | nullable | Dùng tính resume |
| `timeoutAt` | `timestamptz` | nullable | SLA timeout |
| `createdAt` | `timestamptz` | NOT NULL DEFAULT now() | |
| `createdBy` | `varchar(200)` | NOT NULL | |
| `updatedAt` | `timestamptz` | nullable | |
| `updatedBy` | `varchar(200)` | nullable | |
| `rowVersion` | `xmin` | concurrency token | Optimistic concurrency |

Index: `(tenantId, userId, status)`, `(tenantId, shiftId)`, `(tenantId, zoneId, startedAt)`, `(tenantId, sourceTaskType, sourceTaskId)`.
Unique partial index: one active session per user where `status in ('Running','Paused')`.

**`"LaborSessionEvents"`** (immutable audit)

| Column | Type | Constraint | Ghi chú |
|---|---|---|---|
| `id` | `uuid` | PK | |
| `tenantId` | `uuid` | NOT NULL | |
| `sessionId` | `uuid` | NOT NULL | FK `LaborSessions.id` |
| `eventType` | `varchar(40)` | NOT NULL | `Started`, `Paused`, `Resumed`, `Completed`, `Cancelled`, `TimedOut` |
| `actor` | `varchar(200)` | NOT NULL | user hoặc `system` |
| `reason` | `varchar(300)` | nullable | Bắt buộc khi cancel |
| `payload` | `jsonb` | nullable | Snapshot dữ liệu nguồn |
| `traceId` | `varchar(100)` | nullable | Trace ID |
| `occurredAt` | `timestamptz` | NOT NULL DEFAULT now() | |

Index: `(tenantId, sessionId)`, `(tenantId, occurredAt)`, `(tenantId, traceId)`.

**`"LaborShifts"`**

| Column | Type | Constraint | Ghi chú |
|---|---|---|---|
| `id` | `uuid` | PK | |
| `tenantId` | `uuid` | NOT NULL | |
| `userId` | `varchar(200)` | NOT NULL | Username/login hiện có |
| `shiftCode` | `varchar(80)` | NOT NULL | Ví dụ `2026-07-20-DAY-admin` |
| `startedAt` | `timestamptz` | NOT NULL | |
| `endedAt` | `timestamptz` | nullable | |
| `status` | `varchar(30)` | NOT NULL | `Open`, `Closed` |
| `createdAt` | `timestamptz` | NOT NULL DEFAULT now() | |
| `createdBy` | `varchar(200)` | NOT NULL | |

Index: `(tenantId, userId, status)`, `(tenantId, shiftCode)`, `(tenantId, startedAt)`.

**Migration strategy:** Tạo migration `AddLaborTrackingModule` trong `LaborTrackingDbContext` riêng. Không sửa schema `MobileTasks`, `PickTask`, `WavePickTask`, `StorageLocation`. Rollback safe nếu chưa có dữ liệu production: drop 3 bảng LaborTracking.

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

| API | Method | Permission | Ghi chú triển khai |
|---|---|---|---|
| `POST /api/labor/sessions/start` | Start timer | `labor_tracking.create` | Body `{ sourceTaskType, sourceTaskId?, operationType? }`. Gate feature flag. Chặn user có session active. |
| `POST /api/labor/sessions/{id}/pause` | Pause timer | `labor_tracking.update` | Chỉ `Running → Paused`. |
| `POST /api/labor/sessions/{id}/resume` | Resume timer | `labor_tracking.update` | Chỉ `Paused → Running`, cộng `pausedSeconds`. |
| `POST /api/labor/sessions/{id}/complete` | Complete timer | `labor_tracking.update` | Chỉ `Running/Paused → Completed`, tính `durationSeconds`. |
| `POST /api/labor/sessions/{id}/cancel` | Cancel timer | `labor_tracking.update` | Body `{ reason }`; reason bắt buộc. |
| `GET /api/labor/sessions` | Session list | `labor_tracking.read` | Filter `userId`, `status`, `shiftId`, `zoneId`, `from`, `to`, pagination. |
| `GET /api/labor/kpi` | KPI năng suất | `labor_tracking.read` | Filter `userId`, `shiftId`, `zoneId`, `operationType`, `from`, `to`. |
| `GET /api/labor/shifts/current` | Current shift | `labor_tracking.read` | Trả shift đang mở của user; tự tạo khi start nếu chưa có. |

**Request/Response contract tiêu biểu:**

`POST /api/labor/sessions/start`
```json
// Request
{ "sourceTaskType": "MobileTask", "sourceTaskId": "uuid", "operationType": "Picking" }
// Response 200
{ "sessionId": "uuid", "status": "Running", "startedAt": "2026-07-20T02:00:00Z", "shiftId": "uuid" }
// Response 409
{ "errorCode": "LABOR_SESSION_ALREADY_ACTIVE", "message": "User already has an active labor session.", "traceId": "..." }
```

`GET /api/labor/kpi`
```json
{
  "from": "2026-07-20T00:00:00Z",
  "to": "2026-07-20T23:59:59Z",
  "summary": {
    "completedTaskCount": 12,
    "activeSeconds": 3600,
    "pausedSeconds": 300,
    "idleSeconds": 900,
    "averageSecondsPerTask": 300,
    "tasksPerHour": 12.0
  },
  "byUser": [],
  "byShift": [],
  "byZone": [],
  "byOperationType": []
}
```

**Feature flag gate:** Mọi API `/api/labor/*` check `FF_LABOR_TRACKING_ENABLED`; disabled trả `403 FEATURE_DISABLED`.

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

## 7. Frontend/RF/mobile

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| Labor session timer | Start/pause/resume/complete/cancel task | Mobile-first, nút lớn, timer rõ, confirm khi cancel, hiển thị active session hiện tại. |
| Productivity dashboard | Năng suất theo user/shift/zone/operation | KPI cards, chart theo thời gian, filter ngày/ca/user/zone, bảng session drill-down. |
| Labor sessions list | Review lịch sử thao tác | Filter, pagination, status badge, duration, source task link, trace ID. |

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

1. User mở task RF/mobile hoặc supervisor tạo tracking thủ công.
2. UI gọi `POST /api/labor/sessions/start` với `sourceTaskType/sourceTaskId`.
3. Service validate tenant, permission, feature flag, source task tồn tại, user chưa có active session.
4. Service resolve `locationId` và `zoneId` từ source task nếu có.
5. Service lấy shift đang mở hoặc tự tạo `LaborShift` ngày hiện tại.
6. Service tạo `LaborSession(status=Running)` và `LaborSessionEvent(Started)`.
7. User pause/resume nếu bị gián đoạn; hệ thống cộng `pausedSeconds` khi resume.
8. User complete task; service tính `durationSeconds = completedAt - startedAt - pausedSeconds`.
9. API KPI aggregate từ `LaborSessions` completed theo filter user/shift/zone/operation.
10. Timeline observability ghi event quan trọng để hỗ trợ truy vết.

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Một user chỉ có tối đa 1 session active (`Running` hoặc `Paused`) trong cùng tenant.
* `sourceTaskType` chỉ nhận: `MobileTask`, `PickTask`, `WavePickTask`, `Manual`.
* `sourceTaskId` bắt buộc nếu `sourceTaskType != Manual`.
* Source task phải cùng tenant và chưa ở trạng thái terminal không hợp lệ.
* `operationType` chỉ nhận: `Picking`, `Putaway`, `Replenishment`, `Movement`, `Packing`, `Count`, `Manual`.
* Transition hợp lệ: `Running → Paused`, `Paused → Running`, `Running/Paused → Completed`, `Running/Paused → Cancelled`, `Running/Paused → TimedOut`.
* `complete` idempotent-safe: session đã `Completed` trả `409 LABOR_SESSION_INVALID_STATUS`, không cộng duration lần 2.
* `cancel` bắt buộc `reason` khác rỗng.
* `durationSeconds` không âm; nếu clock drift tạo kết quả âm thì trả conflict và ghi trace.
* KPI chỉ tính session `Completed`; session `Cancelled/TimedOut` chỉ vào exception/aging metrics.

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* **Quên complete:** Hosted job đánh dấu `TimedOut` khi session vượt `timeoutAt`; ghi event `TimedOut`, không tự complete.
* **Timeout:** Default timeout 8 giờ từ `startedAt`; có thể cấu hình bằng appsettings/env sau nhưng Phase 28 hardcode default rõ trong service.
* **User đổi ca:** Nếu shift hiện tại đóng khi session đang chạy, session vẫn giữ `shiftId` cũ cho audit; complete vẫn hợp lệ.
* **Task nguồn bị complete/cancel ở module khác:** Labor session không tự sửa task nguồn; nếu source task terminal trước khi labor complete, complete labor vẫn được phép nhưng event payload ghi `sourceTaskStatusAtComplete`.
* **Duplicate start request:** Nếu user có active session, trả `409 LABOR_SESSION_ALREADY_ACTIVE` kèm session hiện tại nếu cùng tenant.
* **Mất mạng RF/mobile:** Không thêm offline queue mới trong Phase 28; UI có thể gọi lại start/complete, backend idempotency dựa trên active-session guard.

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

* **Productivity:** `completedTaskCount`, `tasksPerHour`, `averageSecondsPerTask` theo user/shift/zone/operation.
* **Idle time:** `shift elapsed - activeSeconds - pausedSeconds`, chỉ tính cho shift mở/đóng trong filter.
* **Task aging:** session `Running/Paused` quá SLA và session `TimedOut`.
* **Activity timeline:** ghi `LaborSessionStarted`, `LaborSessionPaused`, `LaborSessionResumed`, `LaborSessionCompleted`, `LaborSessionCancelled`, `LaborSessionTimedOut`.
* **Trace:** mọi command trả `traceId`; log không chứa token/secret.

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

* `verify_labor_tracking.ps1` tự đăng nhập admin, bật feature flag, tạo hoặc dùng `MobileTask` mở, chạy strict API tests.
* Start session từ `MobileTask` hợp lệ → 200 `Running`.
* Start session lần 2 cùng user → 409 `LABOR_SESSION_ALREADY_ACTIVE`.
* Pause → 200 `Paused`; Resume → 200 `Running`.
* Complete → 200 `Completed`, `durationSeconds >= 0`, có event timeline.
* Complete lần 2 → 409 `LABOR_SESSION_INVALID_STATUS`.
* Cancel thiếu reason → 400 validation error.
* KPI endpoint trả `completedTaskCount >= 1` và group by user/shift/zone/operation.
* Feature flag disabled → 403 `FEATURE_DISABLED`; script phục hồi flag bằng `finally`.
* Unauthorized/permission thiếu → 401/403 đúng chuẩn.
* `git diff --check`, backend build, frontend lint pass trước khi ghi hoàn thành.

### Test matrix bắt buộc

| Nhóm test | Nội dung |
|---|---|
| Unit | Rule nghiệp vụ, status transition, validation helper |
| Integration | API + DB transaction + permission + concurrency |
| E2E | Luồng người dùng chính từ UI/RF/mobile |
| Negative | Sai quyền, sai trạng thái, dữ liệu stale, duplicate request |
| Regression | Không phá phase trước và dependency downstream |

### Dữ liệu test

* Tenant demo.
* User đủ quyền và user thiếu quyền.
* Master data hợp lệ và master data inactive.
* Bản ghi đang open/completed/cancelled để test transition.
* Dữ liệu conflict/concurrency nếu phase ghi transaction.

## 13. Acceptance criteria

* Đo được thời gian xử lý task chính qua `LaborSessions.durationSeconds`.
* Supervisor xem được KPI theo user/shift/zone/operation trong dashboard.
* User không thể chạy 2 labor sessions song song trong cùng tenant.
* Pause/resume không làm sai active duration.
* Completed/cancelled/timed-out session có audit event đầy đủ.
* Feature flag và permission chặn đúng mọi endpoint.
* Không sửa schema task nguồn (`MobileTasks`, `PickTask`, `WavePickTask`) trong Phase 28.

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* Payroll integration.
* Incentive/bonus calculation.
* ML productivity scoring.
* Auto task recommendation hoặc task interleaving; thuộc Phase 29.
* Sửa lifecycle của `MobileTask`, `PickTask`, `WavePickTask` ngoài việc đọc trạng thái nguồn.

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

* Phase 09 RF/mobile core scan: `MobileTasks`, handheld/mobile task flow.
* Phase 18 Wave picking: `WavePickTask` source task.
* Phase 25 Operational observability: activity timeline, trace ID, feature flag service.
* Phase 27 Cross-docking đã pass acceptance, không là runtime dependency trực tiếp.
* MasterData `StorageLocation.ZoneId` để group KPI theo zone.

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.

## 16. Maintenance notes

* Automation phải explainable
* Luôn có manual override và reject reason
* Không để tối ưu phá rule an toàn

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

## 17. Extension points

* Tối ưu thuật toán
* Thêm ML/heuristic nâng cao
* Thêm integration thiết bị tự động

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

## 18. Rollback notes

* Tắt `FF_LABOR_TRACKING_ENABLED` để ẩn toàn bộ API/UI Labor.
* Ẩn sidebar/menu Labor bằng permission hoặc feature flag.
* Không xóa session đã ghi trong production; nếu cần thì mark `Cancelled`/`TimedOut` có reason và audit.
* Rollback deployment image trước; xử lý dữ liệu Labor sau theo trace ID.
* Rollback DB chỉ an toàn khi chưa có dữ liệu production: drop `LaborSessionEvents`, `LaborSessions`, `LaborShifts`.

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.





