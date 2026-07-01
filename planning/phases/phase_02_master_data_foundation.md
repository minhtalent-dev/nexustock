# PHASE 02: Master data foundation

## Execution spec maturity

- **Mức hiện tại:** 92%
- **Đánh giá:** Đủ rõ cho master data, import nền, warehouse/zone/location và governance dữ liệu.
- **Khi cần upgrade:** Upgrade nếu phát sinh cấu trúc đa kho đặc thù hoặc import template thay đổi lớn.

## 1. Mục tiêu

Chuẩn hóa dữ liệu nền WMS để mọi nghiệp vụ sau dùng chung một catalog nhất quán.

Phase này thuộc stage **MVP vận hành chắc** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Item, UOM, Package, Warehouse, Zone, Location, Partner, Reason Code, import/export nền, active/inactive lifecycle.

### In scope

* Tạo module MasterData
* Seed reason code phổ biến
* Seed warehouse demo tối thiểu
* Chuẩn hóa permission master-data.*
* Chuẩn hóa DTO camelCase cho frontend.

### Non-negotiable output

* Có database contract hoặc xác nhận không cần database.
* Có API contract hoặc xác nhận chỉ là cấu hình/tài liệu.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Phase 01 hoàn tất. Database và API shell chạy ổn định.

### Readiness checklist

* Phase phụ thuộc đã pass acceptance criteria.
* Master data tối thiểu đã có nếu phase cần dữ liệu vận hành.
* Permission liên quan đã được seed hoặc có kế hoạch seed.
* Không còn migration pending từ phase trước.
* Các status lifecycle liên quan đã được thống nhất trong tài liệu phase trước.

## 4. Setup

* Tạo module MasterData
* Seed reason code phổ biến
* Seed warehouse demo tối thiểu
* Chuẩn hóa permission master-data.*
* Chuẩn hóa DTO camelCase cho frontend.

### Cấu trúc module đề xuất

```text
backend/modules/master_data_foundation/
frontend/features/master_data_foundation/
planning/phases/phase_02_master_data_foundation.md
```

### Permission seed đề xuất

* master_data_foundation.read
* master_data_foundation.create
* master_data_foundation.update
* master_data_foundation.approve
* master_data_foundation.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `Items` | Danh mục hàng hóa | Unique tenantId+itemCode, trackingPolicy, shelfLife, active flag |
| `Uoms` | Đơn vị tính | Unique tenantId+uomCode, base/derived relation |
| `Packages` | Quy cách đóng gói | Liên kết Item/UOM, conversion factor > 0 |
| `Warehouses` | Kho | Unique tenantId+warehouseCode |
| `Zones` | Khu vực trong kho | Thuộc Warehouse, type: storage,qc,staging,shipping,quarantine |
| `Locations` | Vị trí kho | Unique tenantId+locationCode, zoneId, capacity, lock status |
| `Partners` | Nhà cung cấp/khách hàng/vận chuyển | Unique tenantId+partnerCode, partnerType |
| `ReasonCodes` | Lý do chuẩn hóa | Theo reasonType, active flag |

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
| `GET /api/master/items` | Danh sách Item | Filter, paging, sort |
| `POST /api/master/items` | Tạo Item | Validate code, tracking policy |
| `PUT /api/master/items/{id}` | Cập nhật Item | Không đổi code nếu đã phát sinh giao dịch |
| `GET /api/master/locations` | Danh sách vị trí | Filter warehouse/zone/status |
| `POST /api/master/import/preview` | Preview import | Không commit DB |
| `POST /api/master/import/commit` | Commit import | Atomic theo batch hợp lệ |

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
| Item management | Quản lý hàng hóa | List/detail/import/export, badge active |
| Location management | Quản lý warehouse/zone/location | Tree warehouse-zone-location |
| Reason code management | Quản lý lý do | Filter theo reasonType |
| Import preview | Xem lỗi trước khi commit | Hiển thị lỗi theo dòng/cột |

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

1. Admin tạo UOM
2. Tạo Item và package
3. Tạo warehouse, zone, location
4. Tạo partner và reason code
5. Export đối soát dữ liệu nền

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Code unique theo tenant
* Không hard delete dữ liệu đã dùng
* Inactive item không cho phát sinh giao dịch mới
* Location thuộc đúng zone/warehouse
* Conversion factor phải dương

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Trùng code
* Import thiếu cột bắt buộc
* Location sai zone
* Xóa dữ liệu đã dùng
* UOM conversion không hợp lệ

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

* Audit mọi tạo/sửa/inactive
* Import job log số dòng thành công/thất bại
* Trace ID cho mỗi import batch

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

* CRUD Item/UOM/Location
* Import duplicate code
* Inactive item bị chặn ở API tạo giao dịch mẫu
* Pagination không trả quá limit

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

* Tạo được bộ master data đủ chạy inbound
* Import preview báo lỗi rõ
* Không có dữ liệu nền trùng code

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* RBAC chi tiết nâng cao
* Rule engine
* Inventory balance

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

* Phase 01

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.

## 16. Maintenance notes

* Mọi master data mới phải có code, name, active flag, audit fields
* Không sửa trực tiếp dữ liệu bằng SQL trừ script migration có review

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

## 17. Extension points

* Thêm barcode alias
* Thêm item category
* Thêm location coordinate XYZ
* Thêm data governance approval

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

## 18. Rollback notes

* Rollback migration master data nếu chưa có transaction
* Nếu import sai, dùng import rollback report hoặc inactive batch

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.





