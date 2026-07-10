# PHASE 05: QC hold/release

## Execution spec maturity

- **Mức hiện tại:** 100% (Completed Spec)
- **Đánh giá:** Đã hoàn tất chi tiết hóa cấu trúc module, sơ đồ cơ sở dữ liệu (PostgreSQL), danh sách phân quyền đồng bộ với hệ thống Identity/Inbound, và các DTO API contract cụ thể. Sẵn sàng triển khai.
- **Khi cần upgrade:** Upgrade nếu cần quy trình duyệt QC nhiều cấp hoặc tích hợp thiết bị lấy mẫu tự động.

## 1. Mục tiêu

Kiểm soát chất lượng Lot sau nhận: hold, release, reject, quarantine.

Phase này thuộc stage **MVP vận hành chắc** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Kiểm soát chất lượng Lot sau nhận: hold, release, reject, quarantine.

### In scope

* Tạo module QC hold/release (`Nexustock.Modules.Qc`)
* Seed permission và lý do QC
* Cấu hình route/API/menu
* Chuẩn hóa DTO camelCase

### Non-negotiable output

* Có database contract PostgreSQL chi tiết.
* Có API contract và DTO cấu trúc rõ ràng.
* Giao diện UI quản lý danh sách hàng chờ QC, ghi kết quả và hold/release.
* Luồng nghiệp vụ chính chạy được E2E.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Các phase phụ thuộc đã hoàn tất và dữ liệu nền liên quan đã sẵn sàng.

### Readiness checklist

* Phase phụ thuộc (Phase 04 - Inbound) đã pass và chạy thử thành công.
* Master data tối thiểu (vật tư, kho bãi, đối tác) đã sẵn sàng.
* Không còn migration pending từ phase trước.
* Quyền hạn hệ thống và cấu hình menu sidebar đã được quy hoạch.

## 4. Setup

* Tạo module QC hold/release
* Seed permission và lý do QC
* Cấu hình route/API/menu
* Chuẩn hóa DTO camelCase

### Cấu trúc module đề xuất

```text
backend/modules/Nexustock.Modules.Qc/
frontend/src/features/qc/
frontend/src/app/admin/qc/
planning/phases/phase_05_qc_hold_release.md
```

### Permission seed đề xuất

* `Qc.Queue.View` - Xem danh sách hàng/lô chờ kiểm định QC
* `Qc.Results.Create` - Ghi nhận kết quả kiểm định QC
* `Qc.Lots.Hold` - Khóa lô hàng (Hold)
* `Qc.Lots.Release` - Giải phóng lô hàng (Release)
* `Qc.Lots.Reject` - Từ chối/loại bỏ lô hàng (Reject)

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

### Các bảng dữ liệu chi tiết

| Bảng | Trường dữ liệu | Mô tả & Ràng buộc |
|---|---|---|
| `QcRequests` | `Id` (Guid, PK)<br>`TenantId` (Guid)<br>`LotId` (Guid, FK `Lots.Id`)<br>`SamplePlan` (Varchar(100))<br>`Status` (Varchar(50) - Pending, Completed, Cancelled)<br>`CreatedAt` (DateTime), `CreatedBy` (Varchar(100))<br>`UpdatedAt` (DateTime), `UpdatedBy` (Varchar(100)) | Yêu cầu kiểm định chất lượng cho lô hàng. Một Lot chỉ có tối đa 1 yêu cầu QC ở trạng thái `Pending`. |
| `QcResults` | `Id` (Guid, PK)<br>`TenantId` (Guid)<br>`QcRequestId` (Guid, FK `QcRequests.Id`)<br>`IsPassed` (Boolean)<br>`Metrics` (Text - Dữ liệu thông số kiểm tra)<br>`AttachmentRefs` (Text - Link tài liệu/hình ảnh)<br>`Inspector` (Varchar(100))<br>`CreatedAt` (DateTime), `CreatedBy` (Varchar(100)) | Lưu trữ kết quả kiểm định chi tiết của thanh tra viên. |
| `MaterialHolds` | `Id` (Guid, PK)<br>`TenantId` (Guid)<br>`LotId` (Guid, FK `Lots.Id`)<br>`LocationId` (Guid, FK `StorageLocations.Id`, Nullable)<br>`ReasonCode` (Varchar(50))<br>`Status` (Varchar(50) - Active, Released)<br>`HeldBy` (Varchar(100))<br>`ReleasedBy` (Varchar(100), Nullable)<br>`CreatedAt` (DateTime), `CreatedBy` (Varchar(100))<br>`ReleasedAt` (DateTime, Nullable) | Nhật ký và trạng thái khóa hàng hóa (Hold). Nếu LocationId null thì hold toàn bộ Lot, nếu có LocationId thì chỉ hold Lot tại vị trí đó. |

*Lưu ý:* `Lots` từ Phase 04 có trường `QcStatus` dạng Enum/String gồm (`Unspec`, `Hold`, `Release`, `Reject`). Khi ghi nhận kết quả QC hoặc Hold/Release, trạng thái `QcStatus` của thực thể `Lot` sẽ được cập nhật tương ứng.

### Chuẩn database áp dụng

* Mọi bảng nghiệp vụ có `id`, `tenantId`, `createdAt`, `createdBy`, `updatedAt`, `updatedBy` nếu có chỉnh sửa.
* Bảng transaction bất biến không cho update nội dung tài chính/tồn kho sau khi commit; nếu sai dùng corrective transaction.
* Index tối thiểu:
  - `idx_qc_requests_tenant_status` ON `QcRequests` (`TenantId`, `Status`)
  - `idx_material_holds_tenant_lot` ON `MaterialHolds` (`TenantId`, `LotId`, `Status`)
* Dữ liệu số lượng dùng decimal precision thống nhất, không dùng floating point.
* Status lưu bằng enum/string ổn định, không lưu text tự do.
* Migration phải có rollback strategy.

### Transaction boundary

* Mọi thay đổi trạng thái QC của Lot và ghi nhận kết quả/yêu cầu QC phải nằm chung trong một Database Transaction.
* Chống double-submit bằng cách kiểm tra trạng thái QC hiện tại trước khi thực thi.

## 6. Backend/API

### Chi tiết các API và DTO Contract

| API | Phương thức | Mô tả | Quyền yêu cầu | DTO Request / Response |
|---|---|---|---|---|
| `/api/qc/queue` | `GET` | Lấy danh sách lô hàng đang chờ kiểm tra chất lượng (Lot có `QcStatus` = `Unspec` hoặc có `QcRequest` trạng thái `Pending`). | `Qc.Queue.View` | **Response:** `QcQueueResponseDto`<br>- `id` (Guid)<br>- `lotId` (Guid)<br>- `lotNo` (String)<br>- `itemId` (Guid)<br>- `itemName` (String)<br>- `itemCode` (String)<br>- `expectedQty` (Decimal)<br>- `receivedQty` (Decimal)<br>- `createdAt` (DateTime) |
| `/api/qc/{lotId}/result` | `POST` | Ghi nhận kết quả QC (Đạt/Không đạt). Cập nhật `QcStatus` của Lot thành `Release` (nếu pass) hoặc `Reject` (nếu fail). | `Qc.Results.Create` | **Request:** `RecordQcResultDto`<br>- `qcRequestId` (Guid)<br>- `isPassed` (Boolean)<br>- `metrics` (String)<br>- `attachmentRefs` (String)<br>**Response:** 200 OK |
| `/api/qc/{lotId}/hold` | `POST` | Chủ động khóa lô hàng. Cập nhật `QcStatus` của Lot thành `Hold`. Ghi một bản ghi mới vào `MaterialHolds`. | `Qc.Lots.Hold` | **Request:** `HoldLotDto`<br>- `locationId` (Guid, Nullable)<br>- `reasonCode` (String)<br>**Response:** 200 OK |
| `/api/qc/{lotId}/release` | `POST` | Giải phóng lô hàng đang bị khóa (Hold/Reject) về trạng thái khả dụng (`Release`). Cập nhật trạng thái trong `MaterialHolds` thành `Released`. | `Qc.Lots.Release` | **Request:** `ReleaseLotDto`<br>- `reasonCode` (String)<br>**Response:** 200 OK |
| `/api/qc/{lotId}/reject` | `POST` | Chuyển trạng thái lô hàng thành lỗi/hỏng (`Reject`). Chặn xuất kho và chặn di chuyển. | `Qc.Lots.Reject` | **Request:** `RejectLotDto`<br>- `reasonCode` (String)<br>**Response:** 200 OK |
| `/api/storage/upload` | `POST` | Upload tệp tin vật lý (bằng chứng QC, tài liệu) lên thư mục cấu hình tùy chỉnh ngoài project (`UploadSettings:UploadPath` trong `appsettings.json`). | Không yêu cầu (Auth only) | **Request:** Multipart Form (`file`)<br>**Response:** `UploadResponseDto`<br>- `url` (String) |

### Quy chuẩn API

* Request/response dùng camelCase.
* Mutation API bắt buộc auth và permission.
* Response lỗi chuẩn gồm `errorCode`, `message`, `details`, `traceId`.
* Query API có pagination mặc định và max page size.
* Command API validate input tại boundary trước khi vào domain logic.
* Không trả dữ liệu tenant khác, kể cả khi biết id.

### Service layer

* Controller chỉ nhận request, validate model state, gọi application service.
* Application service điều phối transaction, permission, lý do thực hiện.
* Domain service xử lý rule nghiệp vụ: thay đổi trạng thái Lot, kiểm tra tính hợp lệ của thao tác hold/release.

## 7. Frontend/RF/mobile

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| QC queue | Danh sách Lot chờ | Bảng hiển thị các lô hàng mới nhận chưa được QC. Hỗ trợ tìm kiếm nhanh theo mã Lot/vật tư. |
| QC result form | Nhập kết quả | Form trực quan tích chọn "Đạt" hoặc "Không đạt", trường nhập thông số đo lường và tích hợp kéo thả/chọn file tải tệp/ảnh bằng chứng chất lượng gọi API `/api/storage/upload`. |
| Hold/release panel | Lý do và quyền duyệt | Dialog popup xác nhận khi bấm Hold hoặc Release. Bắt buộc người dùng chọn mã lý do (Reason Code) và nhập giải trình ngắn. |

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

1. Lot nhập xong
2. Tạo QC request
3. Inspector kiểm
4. Pass/reject/hold
5. Cập nhật qcStatus
6. Ghi timeline

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Lot hold không được move/pick
* Reject cần reason
* Release cần quyền
* Không sửa result sau approve

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Thiếu reason
* Lot không tồn tại
* Lot đã ship
* Thiếu quyền release

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

* Timeline QC
* Audit hold/release
* KPI pending QC aging

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

* Pass
* Hold chặn pick
* Release mở khóa
* Reject không usable

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

* QC kiểm soát được Lot trước khi tồn usable

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* RMA QC
* Genealogy branch hold

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

* Xem roadmap tổng

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.

## 16. Maintenance notes

* Không bỏ qua audit và permission khi thêm action mới
* Giữ transaction boundary rõ
* Cập nhật phase phụ thuộc nếu đổi status lifecycle

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

## 17. Extension points

* Mở rộng bằng module nâng cao ở stage sau
* Thêm rule engine khi nghiệp vụ cần cấu hình động
* Thêm dashboard khi dữ liệu đủ ổn định

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

## 18. Rollback notes

* Revert migration nếu chưa có dữ liệu thật
* Nếu đã có transaction, dùng corrective transaction thay vì sửa tay
* Tắt permission/menu để rollback chức năng

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.





