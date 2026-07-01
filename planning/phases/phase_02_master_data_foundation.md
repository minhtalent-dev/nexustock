# PHASE 02: Master data foundation

## 1. Mục tiêu

Chuẩn hóa dữ liệu nền WMS để mọi nghiệp vụ sau dùng chung catalog nhất quán, có tenant scope, warehouse scope và lifecycle rõ.

Phase này là nguồn sự thật cho Item, UOM, Package, Warehouse, Zone, Location, Partner và Reason Code.

## 2. Phạm vi

### In scope

* Tạo module Master Data.
* Tạo danh mục Item, UOM, Package, Warehouse, Zone, Location, Partner, Reason Code.
* Seed tenant demo, warehouse demo và reason code phổ biến.
* Chuẩn hóa permission `masterData.*`.
* Chuẩn hóa DTO camelCase cho frontend.
* Tạo import preview/commit và export nền.
* Thiết lập active/inactive lifecycle, không hard delete dữ liệu đã dùng.

### Out of scope

* RBAC nâng cao.
* Inventory balance và ledger.
* Rule engine.
* Receiving transaction.
* Allocation.
* Device integration.

## 3. Dependency

| Loại | Chi tiết |
|---|---|
| Upstream | Phase 01 |
| Downstream trực tiếp | Phase 03-19, 23, 27-30 |
| Contract tạo ra | Master data schema, active/inactive lifecycle, reason code catalog, import/export base |
| Enterprise reference | [Core ERD/schema](../enterprise/core_erd_schema.md), [API contracts core](../enterprise/api_contracts_core.md), [Measurable acceptance criteria](../enterprise/measurable_acceptance_criteria.md) |

## 4. Data ownership

| Entity | Owner | Scope | Downstream usage |
|---|---|---|---|
| Item | Master data admin | tenantId | Receiving, inventory, outbound, allocation |
| UOM | Master data admin | tenantId | Quantity conversion across all workflows |
| Package | Master data admin | tenantId + itemId | Packing, label printing |
| Warehouse | Tenant admin | tenantId | All warehouse workflows |
| Zone | Warehouse admin | tenantId + warehouseId | Putaway, picking, QC, staging |
| Location | Warehouse admin | tenantId + warehouseId | Inventory balance and movement |
| Partner | Master data admin | tenantId | Supplier, customer, carrier, ERP mapping |
| ReasonCode | System/admin | tenantId or system | Override, reject, cancel, adjustment |

## 5. Database

| Table | Required fields | Main constraints | Indexes |
|---|---|---|---|
| `Items` | id, tenantId, itemCode, name, trackingPolicy, shelfLifeDays, status | unique tenantId+itemCode | tenantId+status, tenantId+itemCode |
| `Uoms` | id, tenantId, uomCode, name, baseUomId, conversionFactor, status | conversionFactor > 0 | tenantId+uomCode |
| `Packages` | id, tenantId, itemId, packageCode, uomId, qtyPerPackage, barcode, status | qtyPerPackage > 0 | tenantId+itemId, tenantId+barcode |
| `Warehouses` | id, tenantId, warehouseCode, name, timezone, status | unique tenantId+warehouseCode | tenantId+status |
| `Zones` | id, tenantId, warehouseId, zoneCode, zoneType, status | unique warehouseId+zoneCode | tenantId+warehouseId |
| `Locations` | id, tenantId, warehouseId, zoneId, locationCode, locationType, capacity, status, lockReason | unique warehouseId+locationCode | tenantId+warehouseId+status |
| `Partners` | id, tenantId, partnerCode, partnerType, name, status | unique tenantId+partnerCode | tenantId+partnerType+status |
| `ReasonCodes` | id, tenantId, reasonType, reasonCode, description, requiresApproval, status | unique tenantId+reasonType+reasonCode | tenantId+reasonType+status |
| `ImportJobs` | id, tenantId, importType, fileName, status, totalRows, validRows, invalidRows, traceId | no commit when invalidRows > 0 | tenantId+importType+status |

### Database rules

* Mọi bảng nghiệp vụ có `tenantId`.
* Warehouse-scoped entity phải có `warehouseId`.
* Code unique theo đúng scope.
* Không hard delete dữ liệu đã được transaction downstream tham chiếu.
* Status dùng enum ổn định: `active`, `inactive`, `locked` khi cần.
* Dữ liệu số lượng dùng `decimal(18,6)`, không dùng floating point.
* Migration phải có rollback strategy nếu chưa có data production.

## 6. Backend/API

| API | Mục đích | Permission | Ghi chú |
|---|---|---|---|
| `GET /api/master/items` | Danh sách item | `masterData.item.read` | Filter, paging, sort |
| `POST /api/master/items` | Tạo item | `masterData.item.create` | Validate code và trackingPolicy |
| `PUT /api/master/items/{id}` | Cập nhật item | `masterData.item.update` | Không đổi code nếu đã phát sinh transaction |
| `PATCH /api/master/items/{id}/status` | Active/inactive item | `masterData.item.update` | Requires reason khi inactive |
| `GET /api/master/locations` | Danh sách vị trí | `masterData.location.read` | Filter warehouse/zone/status |
| `POST /api/master/import/{type}/preview` | Preview import | `masterData.import.preview` | Không commit DB |
| `POST /api/master/import/{id}/commit` | Commit import | `masterData.import.commit` | Atomic theo batch hợp lệ |
| `GET /api/master/export/{type}` | Export template/data | `masterData.export` | Có traceId |

### API rules

* Request/response dùng camelCase.
* Mutation API bắt buộc auth, permission và audit.
* Import commit bắt buộc idempotency key.
* Response lỗi chuẩn gồm `errorCode`, `message`, `details`, `traceId`.
* Không trả dữ liệu tenant khác, kể cả khi biết id.

## 7. Frontend/RF/mobile

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| Item management | Quản lý hàng hóa | List/detail/import/export, badge status |
| Location management | Quản lý warehouse/zone/location | Tree warehouse-zone-location |
| Partner management | Quản lý supplier/customer/carrier | Filter theo partnerType |
| Reason code management | Quản lý lý do | Filter theo reasonType |
| Import preview | Xem lỗi trước commit | Hiển thị lỗi theo dòng/cột |

### UI rules

* UI text dùng Sentence case.
* Không dùng inline style.
* Bảng dữ liệu có filter, pagination và trạng thái no result.
* Import preview phải hiển thị tổng dòng, dòng hợp lệ, dòng lỗi, lỗi theo cột.
* Action inactive dữ liệu phải có confirm và reason.

## 8. Execution flow

1. Admin tạo UOM base và conversion.
2. Admin tạo Item và Package.
3. Admin tạo Warehouse, Zone, Location.
4. Admin tạo Partner và Reason Code.
5. Admin import preview file master data.
6. Nếu không có lỗi, commit import bằng idempotency key.
7. Export dữ liệu nền để đối soát.

## 9. Validation & business rules

* Code unique theo tenant hoặc warehouse scope.
* Không hard delete dữ liệu đã dùng.
* Inactive item không cho phát sinh transaction mới.
* Inactive location không được dùng để receive, move, pick.
* Location phải thuộc đúng zone và warehouse.
* Zone type hợp lệ: `storage`, `qc`, `staging`, `shipping`, `quarantine`, `receiving`.
* UOM conversion factor phải dương.
* Package barcode không được trùng trong tenant.
* Reason code inactive không được dùng cho transaction mới.
* Import preview có lỗi thì commit bị chặn.

## 10. Exception handling

| Lỗi | Hành vi hệ thống |
|---|---|
| Trùng code | Trả validation error, chỉ rõ field/scope |
| Import thiếu cột bắt buộc | Preview fail theo dòng/cột |
| Location sai zone | Trả validation error, không commit |
| Xóa dữ liệu đã dùng | Block hard delete, gợi ý inactive |
| UOM conversion không hợp lệ | Trả validation error |
| Dữ liệu stale | Trả 409, yêu cầu reload |
| Thiếu quyền | Trả 403 và ghi audit nếu là mutation |

## 11. Observability

* Audit mọi tạo/sửa/inactive/reactive.
* Import job log tổng dòng, dòng thành công, dòng thất bại.
* Trace ID cho mỗi import batch.
* Activity timeline cho entity chính khi có thay đổi quan trọng.
* Log không chứa secret, token hoặc dữ liệu nhạy cảm không mask.

## 12. Test plan

| Nhóm test | Nội dung |
|---|---|
| Unit | Code uniqueness, UOM conversion, status lifecycle |
| Integration | CRUD Item/UOM/Location, import preview/commit, permission |
| Negative | Duplicate code, inactive item, wrong zone, stale rowVersion |
| E2E | Admin tạo master data đủ để chạy inbound |
| Regression | Phase 01 health/config vẫn hoạt động |

## 13. Measurable acceptance criteria

* Demo tenant có item, UOM, warehouse, zone, location, partner và reason code đủ chạy inbound.
* Duplicate `itemCode` trong cùng tenant bị reject.
* Duplicate `locationCode` trong cùng warehouse bị reject.
* Inactive item không tạo được transaction mới trong API downstream mẫu hoặc contract test.
* Import preview báo lỗi rõ theo dòng/cột và không commit khi còn lỗi.
* Import commit thành công là atomic; fail thì không ghi nửa batch.
* API list có pagination và không vượt max page size.
* Mutation ghi audit và có traceId.

## 14. Definition of done

* Database migration chạy sạch trên database trống.
* API chính có integration test pass.
* UI flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor phase 03-04 hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 15. Maintenance notes

* Mọi master data mới phải có code, name, status, audit fields.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.
* Không sửa trực tiếp dữ liệu bằng SQL trừ migration/script có review.
* Nếu đổi schema master data, cập nhật phase phụ thuộc trong dependency graph.

## 16. Rollback notes

* Rollback migration master data chỉ an toàn nếu chưa có transaction downstream.
* Nếu import sai, dùng import rollback report hoặc inactive batch.
* Không xóa master data đã phát sinh receiving, inventory hoặc outbound transaction.
