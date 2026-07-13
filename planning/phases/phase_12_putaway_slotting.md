# PHASE 12: Putaway slotting

## Execution spec maturity

- **Mức hiện tại:** ✅ Hoàn thành (100% Completed)
- **Đánh giá:** Đã làm chín và bổ sung chi tiết 100% đặc tả kỹ thuật: cấu trúc bảng `PutawayProposals` chi tiết, tích hợp Rule Engine (Phase 11) cho lọc luật `PUTAWAY`, thuật toán tính sức chứa động và chấm điểm vị trí ứng viên, API camelCase chi tiết, giao diện Next.js quản lý đề xuất.
- **Khi cần upgrade:** Upgrade nếu warehouse layout thực tế cần tối ưu hóa quãng đường đi động (Dynamic Routing) phức tạp hơn.

## 1. Mục tiêu

Đề xuất vị trí cất hàng theo rule, capacity, zone và đặc tính vật tư.

Phase này thuộc stage **Advanced WMS** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Đề xuất vị trí cất hàng theo rule, capacity, zone và đặc tính vật tư.

### In scope

* Tạo module Putaway slotting
* Seed permission/rule liên quan
* Cập nhật menu và route

### Non-negotiable output

* Có database contract hoặc xác nhận không cần database.
* Có API contract hoặc xác nhận chỉ là cấu hình/tài liệu.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Stage 1 MVP đã ổn định.

### Readiness checklist

* Phase phụ thuộc đã pass acceptance criteria.
* Master data tối thiểu đã có nếu phase cần dữ liệu vận hành.
* Permission liên quan đã được seed hoặc có kế hoạch seed.
* Không còn migration pending từ phase trước.
* Các status lifecycle liên quan đã được thống nhất trong tài liệu phase trước.

## 4. Setup

* Tạo module Putaway slotting
* Seed permission/rule liên quan
* Cập nhật menu và route

### Cấu trúc module đề xuất

```text
backend/modules/putaway_slotting/
frontend/features/putaway_slotting/
planning/phases/phase_12_putaway_slotting.md
```

### Permission seed đề xuất

* putaway_slotting.read
* putaway_slotting.create
* putaway_slotting.update
* putaway_slotting.approve
* putaway_slotting.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

### Tối ưu cấu trúc bảng (Simplification):
* **Sức chứa & Trạng thái kệ**: Tái sử dụng bảng `Locations` đã chứa `max_capacity`, `max_volume`, `is_locked`, `lock_reason_code`. Hiện trạng sức chứa được tính động bằng cách truy vấn số dư thực tế trong `InventoryBalances`. Bỏ qua việc tạo bảng `LocationCapacities` riêng.
* **Quy tắc cất hàng**: Sử dụng trực tiếp module Rule Engine (Phase 11) với kiểu luật `PUTAWAY`. Bỏ qua việc tạo bảng `SlottingRules`.
* **Bảng Đề xuất cất hàng (`PutawayProposals`)**:

```sql
CREATE TABLE putaway_proposals (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    warehouse_id UUID NOT NULL,
    lot_id UUID NOT NULL,
    item_id UUID NOT NULL,
    qty DECIMAL(18,6) NOT NULL CHECK (qty > 0),
    candidate_location_id UUID NOT NULL,
    score INT NOT NULL DEFAULT 0,
    reason VARCHAR(250),
    status VARCHAR(50) NOT NULL DEFAULT 'SUGGESTED', -- SUGGESTED, CONFIRMED, REJECTED
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    xmin XID NOT NULL -- optimistic concurrency token (RowVersion)
);

CREATE INDEX idx_putaway_proposals_tenant_status ON putaway_proposals(tenant_id, status);
CREATE INDEX idx_putaway_proposals_lot ON putaway_proposals(tenant_id, lot_id);
```

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

### API Endpoints

#### 1. `GET /api/putaway/proposals`
* **Mục đích**: Tính toán và trả về danh sách vị trí cất hàng đề xuất cho một Lô hàng (Lot).
* **Query Params**:
  * `lotId`: UUID (Bắt buộc)
  * `qty`: Decimal (Bắt buộc)
* **Response (camelCase)**:
```json
{
  "lotId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "itemId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "itemCode": "ITEM-CABLE-01",
  "qty": 150.0,
  "proposals": [
    {
      "locationId": "a9bc2561-1234-4567-89ab-cdef01234567",
      "locationCode": "LOC-A-01",
      "zoneCode": "ZONE-NORMAL",
      "score": 80,
      "reason": "Vùng cất hàng ưu tiên (+50), Đang chứa hàng cùng loại (+30)"
    },
    {
      "locationId": "b8cd3472-2345-5678-90bc-def012345678",
      "locationCode": "LOC-A-02",
      "zoneCode": "ZONE-NORMAL",
      "score": 60,
      "reason": "Vị trí trống (+10), Tiện lợi lối đi (+50)"
    }
  ]
}
```

#### 2. `POST /api/putaway/confirm`
* **Mục đích**: Xác nhận cất hàng vào vị trí đã chọn, tạo transaction dịch chuyển kho và tăng tồn kho tại vị trí đích.
* **Request Body (camelCase)**:
```json
{
  "lotId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "qty": 150.0,
  "selectedLocationId": "a9bc2561-1234-4567-89ab-cdef01234567"
}
```
* **Response (camelCase)**:
```json
{
  "success": true,
  "transactionId": "d82bd5d7-1bde-4df8-8097-f58c732cb6a5",
  "message": "Cất hàng vào vị trí LOC-A-01 thành công."
}
```

#### 3. `POST /api/putaway/reject`
* **Mục đích**: Từ chối đề xuất cất hàng, ghi nhận lý do để tối ưu thuật toán hoặc ghi nhận sự cố vị trí.
* **Request Body (camelCase)**:
```json
{
  "lotId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "reasonCode": "LOC_FULL", // Mã lý do từ master data
  "note": "Kệ thực tế đã chật"
}
```
* **Response (camelCase)**:
```json
{
  "success": true,
  "message": "Đã ghi nhận từ chối đề xuất cất hàng."
}
```

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
| Putaway proposal list | Top vị trí đề xuất | Có loading, empty, error, filter, pagination và quyền theo action. |
| Reason panel | Lý do scoring | Có loading, empty, error, filter, pagination và quyền theo action. |

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

### Thuật toán tìm kiếm và chấm điểm ứng viên (Scoring Algorithm):
1. **Lấy danh sách kệ ứng viên**: Truy vấn tất cả vị trí kệ trong kho có trạng thái hoạt động (`isActive = true`) và không bị khóa (`isLocked = false`).
2. **Lọc cứng qua Rule Engine (Phase 11)**:
   - Với mỗi kệ, gửi context (`productGroup`, `locationZone`, `temperatureLimit`, v.v.) vào `RuleEvaluator.EvaluateAsync` của Rules Module.
   - Nếu luật trả về `BLOCK`, loại kệ này ngay lập tức.
3. **Lọc theo Sức chứa (Capacity check)**:
   - Tính tổng số lượng hiện có từ `InventoryBalances` tại vị trí kệ đó: `CurrentQty = SUM(Qty)`.
   - Nếu `CurrentQty + PutawayQty > MaxCapacity` (hoặc kiểm tra `MaxVolume` tương đương), loại kệ này.
4. **Chấm điểm vị trí (Scoring)**:
   - **Vùng cất hàng mặc định (Zone Match)**: Nếu Vùng của kệ trùng với Vùng cất hàng mặc định được cấu hình trên vật tư (`Item.DefaultZone`), cộng 50 điểm.
   - **Gom hàng cùng loại (Product Compatibility)**: Nếu vị trí đang chứa cùng loại vật tư đó (`itemId` đã có số dư tồn kho > 0 tại vị trí này), cộng 30 điểm.
   - **Vị trí trống (Empty Location)**: Nếu vị trí chưa chứa bất kỳ hàng nào (`CurrentQty == 0`), cộng 10 điểm.
   - **Tối ưu quãng đường (Proximity)**: Khoảng cách Manhattan từ vị trí nhận hàng (ví dụ Cổng Nhận) đến kệ càng nhỏ, cộng thêm tối đa 10 điểm.
5. **Sắp xếp & Gợi ý**: Đề xuất danh sách vị trí có điểm số cao nhất cho người dùng.

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Không đề xuất location khóa/quá tải/sai zone
* Hàng nặng ưu tiên thấp
* QC hold bị chặn

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Không có vị trí
* Capacity stale
* Operator reject

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

* Proposal accept rate
* Slot utilization

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

* Capacity
* Zone constraint
* Reject proposal
* No candidate

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

* Đề xuất vị trí có lý do và không vi phạm rule

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* Route optimization 3D

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

* Stage 1 + phase trước trong Stage 2

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.

## 16. Maintenance notes

* Không làm phức tạp MVP
* Feature advanced phải có flag/permission riêng
* Mọi transaction inventory phải atomic

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

## 17. Extension points

* Tối ưu thuật toán
* Thêm dashboard nâng cao
* Thêm rule cấu hình sâu hơn

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

## 18. Rollback notes

* Tắt permission/menu
* Release reservation/task mở nếu rollback
* Không xóa transaction đã phát sinh

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.





