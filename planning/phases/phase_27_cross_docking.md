# PHASE 27: Cross-docking

## Execution spec maturity

- **Mức hiện tại:** 100% hoàn thành.
- **Đánh giá:** Đủ contract rõ để executor triển khai không suy đoán. Đã khóa 9 blind spots phát hiện qua reindex codebase thực tế.
- **rp1 verdict:** Nâng từ 88% → 100% sau khi bổ sung: module naming, Outbound stub blocker, DB table casing, QC Unspec rule, allocation collision policy, staging zone definition, feature flag name và ShipmentItem source.

## rp1 — Blind-spot closure matrix

| Blind spot | Closure |
|---|---|
| Module naming `cross_docking` vs. PascalCase | Dùng `Nexustock.Modules.CrossDocking` — khớp convention toàn dự án |
| `Nexustock.Modules.Outbound` chỉ có `Class1.cs` | **Blocker**: Phase 27 phải tự define `ShipmentOrder`/`ShipmentItem` trong CrossDocking module (read-only snapshot) hoặc scope expand Outbound. Default: snapshot inline, không phụ thuộc Outbound impl |
| `ShipmentId` trong WaveItem nhưng Outbound rỗng | CrossDocking tự query Wave's `WaveItem.ShipmentId` để lấy open demand; không đọc Outbound module |
| Lot → InboundOrder link không có FK trực tiếp | Join qua `InboundOrderItem.ItemId` + `InboundOrderItem.InboundOrderId`; thêm field `InboundOrderItemId` vào `CrossDockCandidates` nếu cần trace back |
| `LotQcStatus.Unspec` có được cross-dock không | **Quyết định**: `Unspec` = block (chưa QC → không cho cross-dock). Chỉ `Release` mới được evaluate |
| DB table casing | Dùng `"CrossDockCandidates"` và `"CrossDockEvents"` — quoted PascalCase theo convention PostgreSQL project |
| Bước 6 "Move staging" không rõ | Staging = cập nhật status candidate thành `Executing` + ghi `CrossDockEvents` timeline; không move inventory transaction trong Phase 27. Inventory adjustment là Phase 30 scope |
| Feature flag name chưa định nghĩa | Flag: `FF_CROSS_DOCKING_ENABLED` — env override và DB row |
| Allocation collision khi wave đã allocate | **Policy**: Evaluate chỉ tìm open WaveItem có `QtyAllocated < QtyExpected`. Không override existing allocation. Accept tạo record nhưng không gọi AllocationService trong Phase 27 |

## 1. Mục tiêu

Đề xuất chuyển tiếp trực tiếp hàng vừa nhận sang đơn xuất phù hợp.

Phase này thuộc stage **Optimization & automation** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Đề xuất chuyển tiếp trực tiếp hàng vừa nhận sang đơn xuất phù hợp.

### In scope

* Tạo module Cross-docking
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

* Tạo module Cross-docking
* Bật feature flag/permission
* Chuẩn hóa KPI

### Cấu trúc module đề xuất

```text
backend/modules/Nexustock.Modules.CrossDocking/
  Nexustock.Modules.CrossDocking.csproj
  DependencyInjection.cs
  Contexts/CrossDockingDbContext.cs
  Contexts/CrossDockingDbContextFactory.cs
  Entities/CrossDockCandidate.cs
  Entities/CrossDockEvent.cs
  Services/ICrossDockingService.cs
  Services/CrossDockingService.cs
  Controllers/CrossDockingController.cs
  DTOs/CrossDockingDtos.cs
  Migrations/
frontend/src/app/admin/cross-docking/
  page.tsx                  (candidate list)
  [id]/page.tsx             (candidate detail + accept/reject)
planning/phases/phase_27_cross_docking.md
tests/verify_cross_docking.ps1
```

**Dependencies `.csproj`:**
- `Nexustock.Modules.Inbound` (Lot, InboundOrder, InboundOrderItem entities)
- `Nexustock.Modules.Wave` (WaveItem — query open demand via ShipmentId)
- `Nexustock.Modules.Identity` (tenant, user context)
- `Nexustock.Modules.Observability` (IFeatureFlagService, ITraceContext, IActivityTimelineService)
- `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11`

### Permission seed đề xuất

* cross_docking.read
* cross_docking.create
* cross_docking.update
* cross_docking.approve
* cross_docking.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

### Schema chi tiết

**`"CrossDockCandidates"`**

| Column | Type | Constraint | Ghi chú |
|---|---|---|---|
| `id` | `uuid` | PK | `gen_random_uuid()` |
| `tenantId` | `uuid` | NOT NULL | Multi-tenant scope |
| `lotId` | `uuid` | NOT NULL, FK → Lot.Id | Lô hàng vừa nhận |
| `inboundOrderItemId` | `uuid` | NOT NULL | Trace back Inbound demand |
| `waveItemId` | `uuid` | NOT NULL | Shipment demand từ WaveItem |
| `itemId` | `uuid` | NOT NULL | SKU cần khớp |
| `qtyAvailable` | `numeric(18,4)` | NOT NULL | Từ Lot, QC Released |
| `qtyRequested` | `numeric(18,4)` | NOT NULL | Từ WaveItem open qty |
| `qtyMatched` | `numeric(18,4)` | NOT NULL | `min(qtyAvailable, qtyRequested)` |
| `matchScore` | `integer` | NOT NULL DEFAULT 0 | 0–100, priority sort |
| `status` | `varchar(30)` | NOT NULL | `Pending`, `Accepted`, `Rejected`, `Expired`, `Executing` |
| `expiresAt` | `timestamptz` | nullable | Candidate hết hạn nếu shipment cancel |
| `rejectedReason` | `text` | nullable | Bắt buộc khi Reject |
| `createdAt` | `timestamptz` | NOT NULL DEFAULT now() | |
| `createdBy` | `varchar(200)` | NOT NULL | |
| `updatedAt` | `timestamptz` | nullable | |
| `updatedBy` | `varchar(200)` | nullable | |

Index: `(tenantId)`, `(lotId)`, `(waveItemId)`, `(status)`, `(createdAt)`.

**`"CrossDockEvents"`** (immutable audit — không UPDATE)

| Column | Type | Constraint | Ghi chú |
|---|---|---|---|
| `id` | `uuid` | PK | |
| `tenantId` | `uuid` | NOT NULL | |
| `candidateId` | `uuid` | NOT NULL, FK → CrossDockCandidates.Id | |
| `eventType` | `varchar(50)` | NOT NULL | `Evaluated`, `Accepted`, `Rejected`, `Expired`, `Executed` |
| `actor` | `varchar(200)` | NOT NULL | userId hoặc `system` |
| `payload` | `jsonb` | nullable | Snapshot data tại thời điểm event |
| `traceId` | `varchar(100)` | nullable | Trace ID từ request |
| `occurredAt` | `timestamptz` | NOT NULL DEFAULT now() | |

Index: `(tenantId, candidateId)`, `(occurredAt)`.

**Migration strategy**: Tạo migration mới `AddCrossDockingModule` trong CrossDockingDbContext riêng. Không đụng schema module khác. Rollback safe — drop table nếu chưa có data production.

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

| API | Method | Permission | Ghi chú |
|---|---|---|---|
| `GET /api/cross-docking/candidates` | Query candidates | `cross_docking.read` | Filter: `lotId`, `status`, `itemId`. Paginated. |
| `POST /api/cross-docking/evaluate` | Tìm ứng viên từ lotId | `cross_docking.create` | Body: `{ lotId }`. Trả danh sách candidates mới tạo. |
| `POST /api/cross-docking/{id}/accept` | Chấp nhận candidate | `cross_docking.approve` | Transition: `Pending → Accepted`. Ghi event. |
| `POST /api/cross-docking/{id}/reject` | Từ chối candidate | `cross_docking.approve` | Body: `{ reason }` — bắt buộc. Transition: `Pending → Rejected`. |
| `GET /api/cross-docking/{id}` | Chi tiết candidate | `cross_docking.read` | Kèm events timeline. |

**Request/Response contract tiêu biểu:**

`POST /api/cross-docking/evaluate`
```json
// Request
{ "lotId": "uuid" }
// Response 200
{ "candidates": [ { "id": "uuid", "itemId": "uuid", "qtyMatched": 10.0, "matchScore": 85, "status": "Pending" } ] }
// Response 400 — lot QcStatus != Release
{ "errorCode": "LOT_NOT_QC_RELEASED", "message": "Lot must have QC status Release.", "traceId": "..." }
// Response 404 — lot không tồn tại
{ "errorCode": "LOT_NOT_FOUND", "message": "Lot not found.", "traceId": "..." }
```

`POST /api/cross-docking/{id}/reject`
```json
// Request
{ "reason": "Shipment priority changed" }
// Response 409 — đã accept/reject rồi
{ "errorCode": "CANDIDATE_INVALID_STATUS", "message": "Candidate is not in Pending status.", "traceId": "..." }
```

**Feature flag gate**: Mọi API phải check `FF_CROSS_DOCKING_ENABLED`; nếu disabled trả `403 FEATURE_DISABLED`.

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
| Cross-dock suggestion | Gợi ý trên receiving | Có loading, empty, error, filter, pagination và quyền theo action. |
| Candidate list | Danh sách ứng viên | Có loading, empty, error, filter, pagination và quyền theo action. |

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

1. Receive Lot
2. Match open shipment
3. Check QC/allocation
4. Create candidate
5. Manager accept
6. Move staging

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Không bypass QC
* Không phá allocation
* Candidate hết hạn nếu shipment đổi

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* QC block
* Shipment cancel
* Qty mismatch

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

* Cross-dock rate
* Time saved

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

* Match
* QC block
* Reject
* Execute

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

- `/api/cross-docking/evaluate` với Lot QcStatus=`Release` trả candidates list 200.
- `/api/cross-docking/evaluate` với Lot QcStatus=`Hold`/`Unspec`/`Reject` trả 400 `LOT_NOT_QC_RELEASED`.
- `/api/cross-docking/{id}/accept` transition `Pending → Accepted`, ghi `CrossDockEvents`.
- `/api/cross-docking/{id}/reject` bắt buộc `reason`, transition `Pending → Rejected`.
- `/api/cross-docking/{id}/accept` trên candidate không phải `Pending` trả 409.
- `FF_CROSS_DOCKING_ENABLED=false` trả 403 cho mọi endpoint.
- Migration `AddCrossDockingModule` chạy sạch trên DB trống.
- Frontend list hiển thị candidates với filter status, loading, empty state.
- `verify_cross_docking.ps1` pass.

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* Dock door assignment

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

**Phase dependencies cụ thể:**
- Phase 01/03 MasterData: `ItemId`, `TenantId` đã sẵn sàng.
- Phase 05 Inbound: `Lot`, `InboundOrder`, `InboundOrderItem` entities có thể query.
- Phase 06 QC: `LotQcStatus` enum đã có — chỉ cho cross-dock khi `Release`.
- Phase 11 Wave: `WaveItem.ShipmentId`, `WaveItem.QtyAllocated`, `WaveItem.QtyExpected` có thể query open demand.
- Phase 26 Feature Flags: `IFeatureFlagService.IsEnabledAsync("FF_CROSS_DOCKING_ENABLED")` đã có.
- Phase 25 Observability: `ITraceContext`, `IActivityTimelineService` đã có.

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

* Tắt `FF_CROSS_DOCKING_ENABLED` qua env — không cần redeploy.
* Candidates đã `Accepted` không xóa — mark `Expired` bằng corrective event.
* Không có inventory transaction trong Phase 27 nên rollback DB không cần corrective.

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.

## 19. Verification commands

```powershell
dotnet build backend/Nexustock.Api/Nexustock.Api.csproj --no-restore
dotnet build backend/modules/Nexustock.Modules.CrossDocking/Nexustock.Modules.CrossDocking.csproj --no-restore
npm run lint --prefix frontend -- --max-warnings 0
powershell -ExecutionPolicy Bypass -File tests/verify_cross_docking.ps1 -BaseUrl http://localhost:5024
git diff --check
```

**`verify_cross_docking.ps1` checks:**
- `POST /api/cross-docking/evaluate` với lot QcStatus=Release → 200 candidates.
- `POST /api/cross-docking/evaluate` với lot QcStatus=Hold → 400 `LOT_NOT_QC_RELEASED`.
- `POST /api/cross-docking/{id}/accept` → 200, ghi event.
- `POST /api/cross-docking/{id}/reject` thiếu reason → 400.
- `GET /api/cross-docking/{id}` → 200 kèm events.
- Mọi endpoint khi `FF_CROSS_DOCKING_ENABLED=false` → 403.
## 20. Execution evidence

* **API Verification:** Đã nâng cấp và chạy [verify_cross_docking.ps1](file:///d:/1_Project/48_Nexustock/tests/verify_cross_docking.ps1) vượt qua 6/6 kịch bản kiểm thử tích hợp nghiêm ngặt (tự động đăng nhập, sinh vị trí kệ tạm LOC-CD-TEST dung lượng lớn, tạo Inbound Order/Lot QC Release/Lot QC Blocked/Shipment/Wave, thực thi evaluate, validate status code, verify detail/timeline events, kiểm thử feature flag).
* **UI Verification:** Kiểm thử E2E giao diện trên browser: Đăng nhập admin -> Đánh giá Lot -> Xem chi tiết timeline -> Duyệt thành công (Accept). Trạng thái cập nhật đồng bộ về cơ sở dữ liệu.
* **Tài liệu bàn giao:** Xem chi tiết [walkthrough.md](file:///C:/Users/mes/.gemini/antigravity-ide/brain/129ed964-eb82-4a5b-98ca-b26ec26ceec2/walkthrough.md) kèm video Web Recording chứng minh thực tế.
