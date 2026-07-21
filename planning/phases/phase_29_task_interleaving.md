# PHASE 29: Task interleaving

## Execution spec maturity

- **Mức hiện tại:** 100%
- **Đánh giá:** Đã triển khai đồng bộ backend, database, API, UI/RF/mobile, migration, seed, frontend route, sidebar, unit test, test integration và nghiệm thu gap-fix rp5.
- **Trạng thái:** ✅ Hoàn thành ngày 2026-07-21.
- **Quyết định scope:** Tạo module riêng `TaskInterleaving`, nhưng tái sử dụng `MobileTasks`, dữ liệu vị trí, Labor Tracking và các task nghiệp vụ hiện có làm nguồn candidate.
- **Nguyên tắc tối giản:** Không làm graph routing/ML. Scoring dùng heuristic explainable, deterministic, dễ test.
- **Evidence:** `dotnet build` 0 lỗi; unit tests 19/19 pass; `npm run lint` 0 lỗi; `tests/verify_task_interleaving.ps1 -SkipFeatureFlagMutation` PASS: 13, SKIP: 2 (worker user chưa seed; flag mutation skip), FAIL: 0. Gap-fix: unique Open index, scoring v1, 6 structured logs, Accept shared-connection TX, admin UI states, mobile Next task.

## 1. Mục tiêu

Gợi ý task kế tiếp an toàn cho user sau khi hoàn tất hoặc đang tìm việc mới, nhằm giảm di chuyển rỗng giữa các vị trí kho mà không phá rule vận hành, quyền, zone, trạng thái task hoặc ràng buộc tồn kho.

Phase này thuộc stage **Optimization & automation** và phải tạo deliverable kiểm thử độc lập:

- Backend module có feature flag, permission, service và controller riêng.
- Database contract ghi nhận recommendation, candidate snapshot và quyết định accept/reject.
- UI/RF/mobile touchpoint cho prompt task kế tiếp và hàng đợi ưu tiên.
- KPI đo accept rate, rejection reason, travel score và task latency.
- Test integration xác nhận end-to-end từ candidate discovery đến accept/reject.

## 2. Phạm vi

### In scope

* Tạo module backend `Nexustock.Modules.TaskInterleaving`.
* Seed feature flag `FF_TASK_INTERLEAVING_ENABLED`.
* Seed permission tối thiểu:
  * `task_interleaving.read`
  * `task_interleaving.accept`
  * `task_interleaving.reject`
* Tạo bảng recommendation log và candidate snapshot.
* Gợi ý task kế tiếp từ nguồn `MobileTasks` đang mở.
* Scoring deterministic dựa trên zone/location distance, task age, task priority, operation continuity và labor context.
* API lấy gợi ý task kế tiếp, accept, reject, xem queue/log.
* UI admin giám sát recommendation logs và KPI.
* RF/mobile prompt sau khi user complete task hoặc nhấn tìm task kế tiếp.
* Integration test cho happy path, no candidate, unauthorized, conflict, stale recommendation, accept/reject.

### Out of scope

* Graph routing/AGV/robot routing.
* ML/AI model scoring.
* Tối ưu multi-worker toàn cục.
* Tự động thay đổi tồn kho hoặc tự complete task nghiệp vụ.
* Thay thế rule engine allocation/replenishment hiện có.
* Rebuild toàn bộ MobileTask lifecycle.

### Non-negotiable output

* Có database contract rõ ràng.
* Có API contract camelCase.
* Có UI/RF/mobile touchpoint.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan cụ thể.
* Không còn placeholder generic trong tài liệu phase.

## 3. Điều kiện đầu vào

Stage trước đã ổn định và có dữ liệu vận hành thực tế.

### Readiness checklist

* Phase 09 RF/mobile core scan đã hoàn tất, có `MobileTasks` và handheld flow.
* Phase 13 Allocation đã hoàn tất, task xuất kho không bị phá reservation.
* Phase 14 Replenishment đã hoàn tất, có task bổ sung pick face.
* Phase 18 Wave picking đã hoàn tất, có task picking theo wave.
* Phase 25 Operational observability đã hoàn tất, có trace/audit/dashboard nền.
* Phase 28 Labor tracking đã hoàn tất, có user/shift/session context.
* Không còn migration pending từ phase trước.
* Permission seed mechanism hiện có hoạt động.

## 4. Setup

### Module structure

```text
backend/modules/Nexustock.Modules.TaskInterleaving/
  Contexts/TaskInterleavingDbContext.cs
  Controllers/TaskInterleavingController.cs
  Dtos/TaskInterleavingDtos.cs
  Entities/TaskRecommendation.cs
  Entities/TaskRecommendationCandidate.cs
  Services/ITaskInterleavingService.cs
  Services/TaskInterleavingService.cs
  DependencyInjection.cs
  Migrations/

frontend/src/lib/task-interleaving-api.ts
frontend/src/app/admin/task-interleaving/page.tsx
frontend/src/app/admin/task-interleaving/components/recommendation-kpis.tsx
frontend/src/app/admin/task-interleaving/components/recommendation-table.tsx
frontend/src/app/mobile/tasks/next/page.tsx

tests/verify_task_interleaving.ps1
planning/phases/phase_29_task_interleaving.md
```

### Feature flag

| Flag | Default dev | Default prod | Mục đích |
|---|:---:|:---:|---|
| `FF_TASK_INTERLEAVING_ENABLED` | `true` | `false` | Bật/tắt toàn bộ API và UI task interleaving. |

### Permission seed

| Permission | Scope dùng |
|---|---|
| `task_interleaving.read` | Xem recommendation, queue, KPI. |
| `task_interleaving.accept` | Nhận task được gợi ý. |
| `task_interleaving.reject` | Từ chối task được gợi ý, bắt buộc reason. |

Không seed `create/update/approve/export` trong phase này vì không có màn hình hoặc API tương ứng.

## 5. Database

### Bảng `TaskRecommendations`

Ghi nhận mỗi lần hệ thống tạo gợi ý cho user.

| Column | Type | Required | Ghi chú |
|---|---|:---:|---|
| `Id` | uuid | Yes | Primary key. |
| `TenantId` | uuid | Yes | Tenant scope. |
| `UserId` | uuid | Yes | User nhận gợi ý. |
| `ShiftId` | uuid nullable | No | Lấy từ Labor Tracking current shift nếu có. |
| `LaborSessionId` | uuid nullable | No | Session hiện hành nếu có. |
| `SourceTaskType` | varchar(64) nullable | No | Task vừa hoàn tất hoặc context hiện tại. |
| `SourceTaskId` | uuid nullable | No | Task context hiện tại. |
| `CurrentLocationId` | uuid nullable | No | Vị trí hiện tại của user/task. |
| `CurrentZoneId` | uuid nullable | No | Zone hiện tại. |
| `Status` | varchar(32) | Yes | `Open`, `Accepted`, `Rejected`, `Expired`, `Superseded`, `NoCandidate`. |
| `SelectedTaskType` | varchar(64) nullable | No | Candidate được score cao nhất. |
| `SelectedTaskId` | uuid nullable | No | Candidate được score cao nhất. |
| `SelectedScore` | decimal(18,4) nullable | No | Điểm cuối cùng. |
| `ReasonCode` | varchar(64) nullable | No | Reject/no-candidate/expired reason. |
| `DecisionNote` | varchar(512) nullable | No | Ghi chú reject/override. |
| `AcceptedAt` | timestamptz nullable | No | Thời điểm accept. |
| `RejectedAt` | timestamptz nullable | No | Thời điểm reject. |
| `ExpiresAt` | timestamptz | Yes | Hạn hiệu lực, mặc định 120 giây. |
| `TraceId` | varchar(128) nullable | No | Trace request. |
| `CreatedAt` | timestamptz | Yes | Audit. |
| `CreatedBy` | varchar(128) | Yes | Audit. |
| `UpdatedAt` | timestamptz nullable | No | Audit. |
| `UpdatedBy` | varchar(128) nullable | No | Audit. |

### Bảng `TaskRecommendationCandidates`

Snapshot candidate tại thời điểm scoring, để recommendation explainable và audit được.

| Column | Type | Required | Ghi chú |
|---|---|:---:|---|
| `Id` | uuid | Yes | Primary key. |
| `TenantId` | uuid | Yes | Tenant scope. |
| `RecommendationId` | uuid | Yes | FK logical tới `TaskRecommendations`. |
| `TaskType` | varchar(64) | Yes | `MobileTask`, `ReplenishmentTask`, `WavePickTask`, future safe. |
| `TaskId` | uuid | Yes | ID task nguồn. |
| `OperationType` | varchar(64) | Yes | Picking, Putaway, Replenishment, CycleCount, Packing. |
| `LocationId` | uuid nullable | No | Vị trí đích. |
| `ZoneId` | uuid nullable | No | Zone đích. |
| `TaskStatus` | varchar(32) | Yes | Snapshot status khi score. |
| `Priority` | int | Yes | Mặc định 0 nếu nguồn không có priority. |
| `AgeSeconds` | int | Yes | Tuổi task tại thời điểm score. |
| `DistanceScore` | decimal(18,4) | Yes | Điểm gần vị trí. |
| `AgeScore` | decimal(18,4) | Yes | Điểm chờ lâu. |
| `PriorityScore` | decimal(18,4) | Yes | Điểm ưu tiên nghiệp vụ. |
| `ContinuityScore` | decimal(18,4) | Yes | Điểm cùng operation/zone/shift. |
| `PenaltyScore` | decimal(18,4) | Yes | Điểm phạt do risk/blocked/stale. |
| `TotalScore` | decimal(18,4) | Yes | Tổng điểm. |
| `Explanation` | jsonb | Yes | Breakdown camelCase. |
| `CreatedAt` | timestamptz | Yes | Audit. |

### Indexes

* `IX_TaskRecommendations_Tenant_User_Status_CreatedAt` trên `(TenantId, UserId, Status, CreatedAt DESC)`.
* `IX_TaskRecommendations_Tenant_SelectedTask` trên `(TenantId, SelectedTaskType, SelectedTaskId)`.
* `IX_TaskRecommendations_Tenant_ExpiresAt` trên `(TenantId, ExpiresAt)`.
* `IX_TaskRecommendationCandidates_Recommendation_TotalScore` trên `(RecommendationId, TotalScore DESC)`.
* Unique partial index: một user chỉ có một recommendation `Open` chưa hết hạn.

### Migration rollback

Rollback an toàn trong dev/staging:

1. Tắt `FF_TASK_INTERLEAVING_ENABLED`.
2. Drop partial index.
3. Drop `TaskRecommendationCandidates`.
4. Drop `TaskRecommendations`.

Production rollback không xóa dữ liệu đã ghi; dùng feature flag để ngừng ghi mới, giữ log để audit.

## 6. Backend/API

Base route: `/api/task-interleaving`

Tất cả response dùng camelCase.

### API matrix

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/api/task-interleaving/next` | `task_interleaving.read` | Tạo/lấy recommendation task kế tiếp cho user hiện tại. |
| `GET` | `/api/task-interleaving/recommendations` | `task_interleaving.read` | Admin xem recommendation logs có filter/pagination. |
| `GET` | `/api/task-interleaving/recommendations/{id}` | `task_interleaving.read` | Xem detail và candidate breakdown. |
| `POST` | `/api/task-interleaving/recommendations/{id}/accept` | `task_interleaving.accept` | User nhận task được gợi ý. |
| `POST` | `/api/task-interleaving/recommendations/{id}/reject` | `task_interleaving.reject` | User từ chối task, bắt buộc reason. |
| `GET` | `/api/task-interleaving/kpi` | `task_interleaving.read` | KPI accept/reject/travel score. |

### `GET /api/task-interleaving/next`

Query:

| Field | Type | Required | Ghi chú |
|---|---|:---:|---|
| `currentLocationId` | uuid | No | Nếu không gửi, service suy luận từ task/session gần nhất. |
| `currentZoneId` | uuid | No | Nếu không gửi, suy luận từ location. |
| `sourceTaskType` | string | No | Context task vừa hoàn tất. |
| `sourceTaskId` | uuid | No | Context task vừa hoàn tất. |
| `operationType` | string | No | Ưu tiên operation hiện tại. |
| `maxCandidates` | int | No | Default 10, max 25. |

Response:

```json
{
  "recommendationId": "uuid",
  "status": "Open",
  "expiresAt": "2026-07-20T08:00:00Z",
  "selected": {
    "taskType": "MobileTask",
    "taskId": "uuid",
    "operationType": "Picking",
    "locationId": "uuid",
    "zoneId": "uuid",
    "score": 92.5,
    "explanation": {
      "distanceScore": 45,
      "ageScore": 20,
      "priorityScore": 15,
      "continuityScore": 12.5,
      "penaltyScore": 0
    }
  },
  "candidates": [],
  "traceId": "trace-id"
}
```

No candidate response vẫn HTTP 200 để RF/mobile hiển thị fallback an toàn:

```json
{
  "recommendationId": "uuid",
  "status": "NoCandidate",
  "selected": null,
  "candidates": [],
  "message": "No eligible task found.",
  "traceId": "trace-id"
}
```

### `POST /recommendations/{id}/accept`

Request:

```json
{
  "idempotencyKey": "string",
  "acceptedTaskVersion": "string"
}
```

Response:

```json
{
  "recommendationId": "uuid",
  "taskType": "MobileTask",
  "taskId": "uuid",
  "status": "Accepted",
  "assignedToUserId": "uuid",
  "acceptedAt": "2026-07-20T08:00:00Z",
  "traceId": "trace-id"
}
```

### `POST /recommendations/{id}/reject`

Request:

```json
{
  "reasonCode": "TOO_FAR",
  "note": "Optional short note"
}
```

Response:

```json
{
  "recommendationId": "uuid",
  "status": "Rejected",
  "reasonCode": "TOO_FAR",
  "rejectedAt": "2026-07-20T08:00:00Z",
  "traceId": "trace-id"
}
```

### Error contract

| Error code | HTTP | Khi nào |
|---|:---:|---|
| `TASK_INTERLEAVING_DISABLED` | 404/403 | Feature flag off. |
| `TASK_RECOMMENDATION_NOT_FOUND` | 404 | Không thấy recommendation cùng tenant. |
| `TASK_RECOMMENDATION_EXPIRED` | 409 | Recommendation quá hạn. |
| `TASK_ALREADY_ASSIGNED` | 409 | Candidate đã được user khác nhận. |
| `TASK_NOT_ELIGIBLE` | 409 | Task không còn open/eligible. |
| `REJECT_REASON_REQUIRED` | 400 | Reject thiếu reason. |
| `UNAUTHORIZED_TASK_INTERLEAVING` | 403 | Thiếu permission. |

## 7. Service layer

### `ITaskInterleavingService`

```csharp
Task<NextTaskRecommendationResponse> GetNextAsync(NextTaskRecommendationQuery query, CancellationToken ct);
Task<TaskRecommendationDetailResponse> GetDetailAsync(Guid id, CancellationToken ct);
Task<PagedResult<TaskRecommendationListItemDto>> ListAsync(TaskRecommendationListQuery query, CancellationToken ct);
Task<AcceptTaskRecommendationResponse> AcceptAsync(Guid id, AcceptTaskRecommendationRequest request, CancellationToken ct);
Task<RejectTaskRecommendationResponse> RejectAsync(Guid id, RejectTaskRecommendationRequest request, CancellationToken ct);
Task<TaskInterleavingKpiResponse> GetKpiAsync(TaskInterleavingKpiQuery query, CancellationToken ct);
```

### Candidate source v1

Nguồn chính: `InventoryDbContext.MobileTasks`.

Candidate eligible khi:

* `TenantId` trùng tenant hiện tại.
* `Status` thuộc `Open`, `Pending`, hoặc status hiện có tương đương chưa được nhận.
* Không bị locked/assigned cho user khác.
* Có operation type hợp lệ.
* Không bị module nghiệp vụ nguồn đánh dấu completed/cancelled.
* Nếu có zone restriction, user phải được phép thao tác zone đó.

### Scoring formula v1

`totalScore = distanceScore + ageScore + priorityScore + continuityScore - penaltyScore`

| Thành phần | Điểm tối đa | Cách tính |
|---|:---:|---|
| `distanceScore` | 45 | Cùng location 45, cùng zone 35, khác zone 10, thiếu tọa độ 20. |
| `ageScore` | 20 | `min(20, ageMinutes / 3)`. |
| `priorityScore` | 20 | Map priority nguồn: High 20, Medium 10, Low 5, none 0. |
| `continuityScore` | 15 | Cùng operation 8, cùng shift/session context 4, cùng zone 3. |
| `penaltyScore` | 50 | Stale 20, gần expire 10, thiếu location 5, conflict risk 50. |

Tie-breaker deterministic:

1. `totalScore` cao hơn.
2. `priorityScore` cao hơn.
3. `ageSeconds` cao hơn.
4. `TaskId` ascending để kết quả stable.

### Transaction boundary

* `GET /next` được phép tạo log recommendation và candidates trong transaction ngắn.
* `accept` lock recommendation row và task source trong cùng transaction.
* Không giữ DB transaction khi gọi service ngoài.
* Không ghi inventory transaction trong Phase 29.
* Nếu accept thành công, chỉ cập nhật trạng thái assign/task ownership nếu bảng nguồn hỗ trợ an toàn.
* Nếu bảng nguồn không có ownership field, Phase 29 chỉ ghi recommendation accepted và trả task để client tiếp tục flow hiện có.

### Idempotency

* `accept` bắt buộc `idempotencyKey`.
* Cùng `(TenantId, RecommendationId, IdempotencyKey)` trả lại kết quả cũ.
* Reject không cần idempotency key nhưng nhiều lần reject cùng recommendation phải idempotent.

## 8. Frontend/RF/mobile

### Admin UI

Route: `/admin/task-interleaving`

Mục tiêu:

* Xem KPI tổng quan.
* Xem recommendation logs.
* Filter theo user, status, operation, zone, date range.
* Mở detail candidate score breakdown.
* Hiển thị traceId để support vận hành.

UI states bắt buộc:

* Loading.
* Empty.
* Error.
* Unauthorized.
* Feature flag disabled.

### RF/mobile UI

Route: `/mobile/tasks/next`

Touchpoint:

* Sau khi complete task trong handheld, hiển thị card `Suggested next task`.
* User có thể bấm `Accept task`, `Skip`, hoặc `Find another task`.
* Reject/skip bắt buộc chọn reason nhanh.
* Auto-focus scan input nếu task tiếp theo cần scan.
* Font lớn, ít nút, tương tác một tay.

### UI text standard

UI text giữ English, Sentence case:

* `Suggested next task`
* `Accept task`
* `Skip suggestion`
* `No eligible task found`
* `Task already assigned`
* `Recommendation expired`

Không dùng inline style. Tách component rõ, ưu tiên component hiện có.

## 9. Execution flow

### Flow A: User complete task và nhận gợi ý

1. User complete task trong RF/mobile flow hiện có.
2. Client gọi `GET /api/task-interleaving/next` với `sourceTaskType`, `sourceTaskId`, `currentLocationId` nếu có.
3. API validate feature flag, permission, tenant, query limit.
4. Service xác định user, shift, labor session và current location.
5. Service query candidate từ `MobileTasks` eligible.
6. Service score candidates bằng formula v1.
7. Service ghi `TaskRecommendations` và `TaskRecommendationCandidates`.
8. API trả selected candidate và explanation.
9. UI hiển thị prompt.
10. User accept hoặc skip.

### Flow B: Accept recommendation

1. Client gọi `POST /recommendations/{id}/accept` kèm `idempotencyKey`.
2. API validate permission và body.
3. Service load recommendation cùng tenant.
4. Service kiểm tra status `Open`, chưa expired.
5. Service revalidate source task còn eligible.
6. Service lock/assign task nếu nguồn hỗ trợ.
7. Service cập nhật recommendation `Accepted`.
8. Service ghi audit/timeline nếu framework hiện có hỗ trợ.
9. API trả task assignment.
10. UI điều hướng user vào task flow hiện có.

### Flow C: Reject recommendation

1. Client gọi `POST /recommendations/{id}/reject`.
2. API validate `reasonCode` bắt buộc.
3. Service load recommendation cùng tenant.
4. Nếu đã accepted, trả conflict.
5. Nếu open/expired, cập nhật `Rejected` hoặc giữ expired theo rule.
6. Ghi `DecisionNote`, `ReasonCode`, `RejectedAt`.
7. API trả response.
8. UI cho phép user tìm task khác.

### Flow D: No candidate

1. Service không tìm được task eligible.
2. Ghi recommendation status `NoCandidate`.
3. API trả HTTP 200 với `selected = null`.
4. UI hiển thị fallback: refresh queue, quay lại task list, hoặc báo supervisor.

## 10. Validation & business rules

### Boundary validation

* `maxCandidates` từ 1 đến 25.
* UUID fields hợp lệ.
* `sourceTaskType` chỉ nhận giá trị allowlist.
* `operationType` chỉ nhận operation allowlist.
* Reject phải có `reasonCode`.
* `reasonCode` phải thuộc allowlist.

### Business rules

* Không gợi ý task tenant khác.
* Không gợi ý task đã completed/cancelled.
* Không gợi ý task bị locked bởi user khác.
* Không gợi ý task user không có quyền thao tác.
* Không accept recommendation đã expired.
* Không accept candidate nếu task status đã đổi sau khi recommendation tạo.
* Không tự động override rule nghiệp vụ nguồn.
* Không tự động sửa tồn kho.
* Scoring phải explainable trong `Explanation`.
* Reject phải có reason để đo KPI.

### Reason codes v1

| Code | Dùng cho | Ý nghĩa |
|---|---|---|
| `TOO_FAR` | Reject | User thấy task quá xa. |
| `BLOCKED_LOCATION` | Reject | Vị trí đang bị chặn. |
| `EQUIPMENT_UNAVAILABLE` | Reject | Thiếu thiết bị cần thiết. |
| `TASK_CONTEXT_SWITCH` | Reject | Không phù hợp luồng đang làm. |
| `SUPERVISOR_OVERRIDE` | Reject | Supervisor đổi hướng xử lý. |
| `NO_ELIGIBLE_TASK` | NoCandidate | Không có task hợp lệ. |
| `TASK_EXPIRED` | Expired | Recommendation quá hạn. |
| `TASK_CONFLICT` | Conflict | Task đã được user khác nhận. |

## 11. Exception handling

| Nhóm lỗi | Hành vi hệ thống |
|---|---|
| Feature flag off | Trả disabled response/403 tùy API, UI ẩn entry. |
| Input sai | Trả validation error, không ghi transaction. |
| Thiếu quyền | Trả 403, ghi security audit nếu framework sẵn có. |
| No candidate | Ghi `NoCandidate`, trả 200 với selected null. |
| Recommendation expired | Trả 409, UI yêu cầu refresh. |
| Conflict assignment | Trả 409, chuyển recommendation `Superseded` nếu phù hợp. |
| Source task stale | Trả 409, không assign. |
| Lỗi không khôi phục | Rollback transaction, log traceId. |

Không nuốt lỗi âm thầm. Mọi override/reject có reason và audit.

## 12. Observability

### Logs

Log event chính:

* `task_interleaving.recommendation.created`
* `task_interleaving.recommendation.no_candidate`
* `task_interleaving.recommendation.accepted`
* `task_interleaving.recommendation.rejected`
* `task_interleaving.recommendation.expired`
* `task_interleaving.recommendation.conflict`

Log không chứa password, token, secret hoặc dữ liệu nhạy cảm không mask.

### KPI

| KPI | Công thức |
|---|---|
| Accept rate | Accepted / Open recommendations đã quyết định. |
| Reject rate | Rejected / Open recommendations đã quyết định. |
| No candidate rate | NoCandidate / total recommendations. |
| Average selected score | Avg `SelectedScore`. |
| Average decision seconds | Avg thời gian từ created đến accepted/rejected. |
| Conflict rate | Conflict / accept attempts. |
| Same-zone suggestion rate | Candidate cùng zone / total selected. |

### Trace/audit

* Mỗi API response có `traceId`.
* Recommendation detail hiển thị `traceId`.
* Accept/reject ghi actor và thời điểm.
* Candidate snapshot giữ nguyên score để audit sau này không phụ thuộc dữ liệu nguồn đã đổi.

## 13. Test plan

### Automated script

Tạo `tests/verify_task_interleaving.ps1`.

Kịch bản bắt buộc:

1. Feature flag on, user có quyền gọi `GET /next` thành công.
2. User thiếu `task_interleaving.read` nhận 403.
3. No candidate trả HTTP 200, `selected = null`, status `NoCandidate`.
4. Candidate cùng zone được score cao hơn candidate khác zone khi priority tương đương.
5. Candidate priority cao thắng khi distance không quá kém.
6. `GET /next` tạo recommendation log và candidate snapshots.
7. `POST /accept` thành công với idempotency key.
8. Gọi lại `POST /accept` cùng idempotency key trả kết quả cũ.
9. Accept recommendation expired trả 409.
10. Accept khi task đã bị assign user khác trả 409.
11. Reject thiếu reason trả 400.
12. Reject hợp lệ cập nhật status `Rejected` và reason code.
13. List recommendation admin có pagination/filter.
14. KPI endpoint trả accept rate/reject rate/no candidate rate.
15. Feature flag off chặn API đúng chuẩn.

### Unit tests tối thiểu

* Scoring formula deterministic.
* Tie-breaker stable.
* Reason code validation.
* Expiration validation.
* Candidate eligibility filter.

### E2E/UI verification

* Admin page load được KPI và logs.
* RF/mobile `/mobile/tasks/next` hiển thị suggested task.
* Accept task điều hướng vào task flow hiện có.
* Skip suggestion bắt buộc reason.
* Empty state hiển thị `No eligible task found`.

## 14. Acceptance criteria

Phase 29 chỉ được đánh dấu hoàn thành khi:

* Database migration chạy sạch trên database trống.
* Module `Nexustock.Modules.TaskInterleaving` build pass.
* API chính có integration test pass 100%.
* UI admin và RF/mobile flow thao tác được end-to-end.
* Feature flag off chặn API/UI đúng chuẩn.
* Permission guard hoạt động cho read/accept/reject.
* Recommendation log và candidate snapshot ghi đúng.
* Scoring explanation hiển thị được.
* Accept/reject/expired/conflict path được test.
* `npm run lint` pass.
* `dotnet build` pass hoặc có bằng chứng lock-free build nếu dev server đang khóa DLL.
* `tests/verify_task_interleaving.ps1` pass 100% hoặc skip có lý do hợp lệ.
* Phase note và master plan cập nhật sau nghiệm thu.

## 15. Dependencies

### Upstream

* Phase 09 RF/mobile core scan: nguồn `MobileTasks` và handheld UX.
* Phase 13 Allocation: không phá reservation/outbound constraints.
* Phase 14 Replenishment: task bổ sung là candidate hợp lệ.
* Phase 18 Wave picking: picking tasks là candidate hợp lệ nếu expose qua MobileTasks.
* Phase 25 Observability: trace/audit/KPI nền.
* Phase 28 Labor tracking: user, shift, active session context.

### Downstream impact

* Phase 30 được phép dùng KPI và logs của Phase 29 để đánh giá readiness.
* Nếu đổi status/API sau Phase 29, phải cập nhật UI/RF/mobile và verify script.
* Không đổi tên bảng/API đã được Phase 30 tham chiếu nếu không có migration plan.

## 16. Maintenance notes

* Automation phải explainable.
* Luôn có manual reject và reject reason.
* Không để tối ưu phá rule an toàn.
* Giữ scoring formula v1 trong tài liệu cho đến khi có v2.
* Khi thêm source task mới, phải bổ sung eligibility rule, score mapping và test case.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Candidate snapshot không được sửa sau khi tạo, chỉ recommendation decision được cập nhật.

## 17. Extension points

* Tối ưu thuật toán route bằng distance matrix.
* Thêm zone skill matrix.
* Thêm supervisor strategy rule theo ca.
* Thêm ML/ranking service khi có đủ dữ liệu thật.
* Thêm export KPI khi Phase 30 cần báo cáo UAT.

Nguyên tắc mở rộng:

* Ưu tiên rule/config trước hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu EF Core/standard library đủ xử lý.
* Feature nâng cao phải có feature flag hoặc permission riêng.
* Không làm v2 scoring trước khi v1 có evidence vận hành.

## 18. Rollback notes

### Rollback bằng feature flag

1. Tắt `FF_TASK_INTERLEAVING_ENABLED`.
2. Ẩn menu `/admin/task-interleaving` và `/mobile/tasks/next`.
3. API trả disabled/forbidden response.
4. Giữ recommendation logs để audit.

### Rollback deployment

* Rollback image trước nếu API/UI lỗi.
* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu recommendation sai, đánh dấu `Superseded` hoặc giữ log audit, không hard-delete.
* Nếu UI lỗi, ẩn menu/permission tạm thời.
* Nếu API lỗi, xử lý dữ liệu sau theo trace ID.

## 19. Implementation order

1. Tạo backend module và DbContext.
2. Tạo entities + migration.
3. Seed feature flag và permissions.
4. Implement scoring service v1.
5. Implement API controller.
6. Implement integration verify script.
7. Implement frontend API client.
8. Implement admin page.
9. Implement RF/mobile next task page.
10. Run validation gates.
11. Cập nhật phase note/master plan sau nghiệm thu.

## 20. Backend implementation checklist

### Project registration

* Tạo `backend/modules/Nexustock.Modules.TaskInterleaving/Nexustock.Modules.TaskInterleaving.csproj` target `net8.0`.
* Thêm project reference vào [Nexustock.Api.csproj](file:///d:/1_Project/48_Nexustock/backend/Nexustock.Api/Nexustock.Api.csproj).
* Gọi `AddTaskInterleavingModule(builder.Configuration)` trong [Program.cs](file:///d:/1_Project/48_Nexustock/backend/Nexustock.Api/Program.cs) cùng nhóm module WMS.
* Mapping controller phải nằm dưới `/api/task-interleaving`.
* Module dùng `UseNpgsql(configuration.GetConnectionString("Default"))` nhất quán các module hiện có.

### Required files

| File | Nội dung bắt buộc |
|---|---|
| `TaskInterleavingDbContext.cs` | DbSet, tenant query filter, indexes, precision, status max length. |
| `TaskRecommendation.cs` | Entity recommendation header. |
| `TaskRecommendationCandidate.cs` | Entity candidate snapshot. |
| `TaskInterleavingDtos.cs` | Query/request/response DTO camelCase-ready. |
| `ITaskInterleavingService.cs` | Contract service đã nêu ở section 7. |
| `TaskInterleavingService.cs` | Eligibility, scoring, persistence, accept/reject. |
| `TaskInterleavingController.cs` | Auth, permission, feature flag, API endpoints. |
| `DependencyInjection.cs` | DbContext + service registration. |

### Status constants

Dùng constants nội bộ, không rải string tự do:

* Recommendation status: `Open`, `Accepted`, `Rejected`, `Expired`, `Superseded`, `NoCandidate`.
* Candidate task source v1: `MobileTask`.
* Operation allowlist v1: `Picking`, `Putaway`, `Replenishment`, `CycleCount`, `Packing`, `Receiving`.

### Controller guard order

1. Authenticated user.
2. Feature flag `FF_TASK_INTERLEAVING_ENABLED`.
3. Permission theo action.
4. Model validation.
5. Tenant scope.
6. Service execution.
7. Standard error response có `traceId`.

## 21. Frontend implementation checklist

### Required files

| File | Nội dung bắt buộc |
|---|---|
| `frontend/src/lib/task-interleaving-api.ts` | API client typed cho next/list/detail/accept/reject/kpi. |
| `frontend/src/app/admin/task-interleaving/page.tsx` | Dashboard KPI + table + filters. |
| `frontend/src/app/admin/task-interleaving/components/recommendation-kpis.tsx` | KPI cards. |
| `frontend/src/app/admin/task-interleaving/components/recommendation-table.tsx` | Table logs + score breakdown trigger. |
| `frontend/src/app/mobile/tasks/next/page.tsx` | RF/mobile suggested next task prompt. |

### Navigation contract

* Admin menu label: `Task interleaving`.
* Mobile label: `Next task`.
* UI text English only.
* Không inline style.
* Loading/empty/error/unauthorized/feature disabled state đầy đủ.
* Action buttons có id ổn định để browser test:
  * `task-interleaving-refresh-button`
  * `task-interleaving-accept-button`
  * `task-interleaving-reject-button`
  * `task-interleaving-find-another-button`

## 22. Seed and configuration checklist

* Seed `FF_TASK_INTERLEAVING_ENABLED` theo cơ chế feature flag hiện có.
* Seed permissions:
  * `task_interleaving.read`
  * `task_interleaving.accept`
  * `task_interleaving.reject`
* Gán permissions vào admin/supervisor role demo nếu project seed hiện có hỗ trợ.
* Không seed quyền dư.
* Nếu môi trường verify không có endpoint mutate feature flag, test flag-off được phép skip có ghi rõ lý do.

## 23. Definition of Ready 100%

Phase 29 đạt Ready 100% vì đã khóa đủ:

* Module boundary.
* DB entities, columns, indexes và rollback.
* API routes, permissions, DTO shape và error codes.
* Service contract, scoring formula, tie-breaker và transaction boundary.
* UI/RF routes, labels, states và test IDs.
* Feature flag, permission seed và config.
* Integration test matrix 15 kịch bản.
* Acceptance criteria và rollback notes.

Không còn câu hỏi mở chặn triển khai.

## 24. Execution exit gate

Phase 29 đã được mark hoàn thành sau nghiệm thu gap-fix với evidence:

* `dotnet build` pass 0 lỗi.
* Unit tests `Nexustock.TaskInterleaving.UnitTests` 19/19 pass.
* `npm run lint` pass 0 lỗi.
* `tests/verify_task_interleaving.ps1 -SkipFeatureFlagMutation` PASS: 13, SKIP: 2 (worker chưa seed; flag mutation skip hợp lệ), FAIL: 0.
* Unique index `uq_recommendations_tenant_user_open` live; scoring v1; 6 structured log events; Accept shared-connection TX.
* Admin page states + mobile hub `Next task`.
* [IMPLEMENTATION_PLAN.md](file:///d:/1_Project/48_Nexustock/planning/IMPLEMENTATION_PLAN.md) Phase 29 `✅ Hoàn thành` ngày 2026-07-21.
