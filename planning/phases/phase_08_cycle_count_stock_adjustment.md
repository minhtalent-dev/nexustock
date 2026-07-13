# PHASE 08: Cycle count & stock adjustment

## Execution spec maturity

- **Mức hiện tại:** ✅ Hoàn thành (100% Completed)
- **Đánh giá:** Đã triển khai và xác thực thành công. Hoàn tất cấu trúc Database schema PostgreSQL, API contracts chi tiết cho nghiệp vụ kiểm kê chu kỳ, cơ chế tự động khóa/mở khóa vị trí kiểm kê (`location_locks`), ghi nhận kết quả và duyệt chênh lệch, tự động sinh phiếu điều chỉnh và cập nhật tồn kho kèm ledger transactions (`ADJ_IN`/`ADJ_OUT`), cấu hình giao diện Next.js UI. Tích hợp kiểm thử tự động pass 100%.
- **Ngày hoàn thành:** 2026-07-13
- **Khi cần upgrade:** Upgrade nếu kiểm kê cần blind count, recount nhiều vòng hoặc phê duyệt nhiều cấp.

## 1. Mục tiêu

Kiểm kê chu kỳ, khóa vị trí, ghi nhận chênh lệch và phê duyệt điều chỉnh tồn.

Phase này thuộc stage **MVP vận hành chắc** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Kiểm kê chu kỳ, khóa vị trí, ghi nhận chênh lệch và phê duyệt điều chỉnh tồn.

### In scope

* Tạo module Cycle count & stock adjustment
* Seed permission và reason code liên quan
* Cấu hình route/API/menu
* Chuẩn hóa DTO camelCase

### Non-negotiable output

* Có database contract hoặc xác nhận không cần database.
* Có API contract hoặc xác nhận chỉ là cấu hình/tài liệu.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Các phase phụ thuộc đã hoàn tất và dữ liệu nền liên quan đã sẵn sàng.

### Readiness checklist

* Phase phụ thuộc đã pass acceptance criteria.
* Master data tối thiểu đã có nếu phase cần dữ liệu vận hành.
* Permission liên quan đã được seed hoặc có kế hoạch seed.
* Không còn migration pending từ phase trước.
* Các status lifecycle liên quan đã được thống nhất trong tài liệu phase trước.

## 4. Setup

* Tạo module Cycle count & stock adjustment
* Seed permission và reason code liên quan
* Cấu hình route/API/menu
* Chuẩn hóa DTO camelCase

### Cấu trúc module đề xuất

```text
backend/modules/cycle_count_stock_adjustment/
frontend/features/cycle_count_stock_adjustment/
planning/phases/phase_08_cycle_count_stock_adjustment.md
```

### Permission seed đề xuất

* Inventory.CycleCount.View
* Inventory.CycleCount.Create
* Inventory.CycleCount.Count
* Inventory.CycleCount.Approve.L1
* Inventory.CycleCount.Approve.L2
* Inventory.CycleCount.Approve.L3

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `Stocktakes` | Đợt kiểm kê | Scope, status, startedAt |
| `StocktakeItems` | Dòng kiểm kê | SystemQty, countedQty, variance |
| `StockAdjustments` | Phiếu điều chỉnh | Reason, approvalStatus |
| `StockAdjustmentItems` | Chi tiết điều chỉnh | DeltaQty, lot/location |

### Cấu trúc Schema chi tiết (PostgreSQL)

```sql
-- 1. Bảng Đợt kiểm kê (Stocktakes)
CREATE TABLE stocktakes (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    stocktake_no character varying(100) NOT NULL,
    status character varying(50) NOT NULL DEFAULT 'Draft', -- 'Draft', 'Counting', 'Pending_L1_Approve', 'Pending_L2_Approve', 'Pending_L3_Approve', 'Approved', 'Cancelled'
    zone_id uuid,
    total_variance_amount numeric(18,4) NOT NULL DEFAULT 0.0000,
    current_approval_level integer NOT NULL DEFAULT 0,
    started_at timestamp with time zone,
    started_by character varying(100),
    completed_at timestamp with time zone,
    completed_by character varying(100),
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_stocktakes" PRIMARY KEY (id)
);

CREATE UNIQUE INDEX uq_stocktakes_tenant_no ON stocktakes (tenant_id, stocktake_no);

-- 2. Bảng chi tiết dòng kiểm kê (StocktakeItems)
CREATE TABLE stocktake_items (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    stocktake_id uuid NOT NULL,
    location_id uuid NOT NULL,
    item_id uuid NOT NULL,
    lot_no character varying(100) NOT NULL,
    system_qty numeric(18,4) NOT NULL DEFAULT 0.0000,
    counted_qty numeric(18,4),
    variance_qty numeric(18,4),
    status character varying(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Counted', 'RecountRequested'
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_stocktake_items" PRIMARY KEY (id),
    CONSTRAINT "FK_stocktake_items_stocktakes" FOREIGN KEY (stocktake_id) REFERENCES stocktakes(id) ON DELETE CASCADE,
    CONSTRAINT "CK_stocktake_items_system_qty" CHECK (system_qty >= 0),
    CONSTRAINT "CK_stocktake_items_counted_qty" CHECK (counted_qty IS NULL OR counted_qty >= 0)
);

CREATE UNIQUE INDEX uq_stocktake_items_tenant_take_loc_item_lot ON stocktake_items (tenant_id, stocktake_id, location_id, item_id, lot_no);

-- 3. Bảng Phiếu điều chỉnh tồn kho (StockAdjustments)
CREATE TABLE stock_adjustments (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    stocktake_id uuid NOT NULL,
    adjustment_no character varying(100) NOT NULL,
    status character varying(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Applied', 'Rejected'
    approved_at timestamp with time zone,
    approved_by character varying(100),
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_stock_adjustments" PRIMARY KEY (id),
    CONSTRAINT "FK_stock_adjustments_stocktakes" FOREIGN KEY (stocktake_id) REFERENCES stocktakes(id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX uq_stock_adjustments_tenant_no ON stock_adjustments (tenant_id, adjustment_no);

-- 4. Bảng chi tiết điều chỉnh tồn kho (StockAdjustmentItems)
CREATE TABLE stock_adjustment_items (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    adjustment_id uuid NOT NULL,
    location_id uuid NOT NULL,
    item_id uuid NOT NULL,
    lot_no character varying(100) NOT NULL,
    before_qty numeric(18,4) NOT NULL DEFAULT 0.0000,
    after_qty numeric(18,4) NOT NULL DEFAULT 0.0000,
    delta_qty numeric(18,4) NOT NULL DEFAULT 0.0000, -- after_qty - before_qty
    reason_code character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_stock_adjustment_items" PRIMARY KEY (id),
    CONSTRAINT "FK_stock_adjustment_items_adjustments" FOREIGN KEY (adjustment_id) REFERENCES stock_adjustments(id) ON DELETE CASCADE,
    CONSTRAINT "CK_stock_adjustment_items_before_qty" CHECK (before_qty >= 0),
    CONSTRAINT "CK_stock_adjustment_items_after_qty" CHECK (after_qty >= 0)
);

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
| `POST /api/stocktakes` | Tạo kiểm kê | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/stocktakes/{id}/count` | Ghi count | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/stocktakes/{id}/approve` | Duyệt chênh lệch | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/adjustments/{id}/apply` | Áp dụng điều chỉnh | Có auth, validation, trace ID và response lỗi chuẩn. |

### API Contracts và DTOs

#### 1. Lấy danh sách đợt kiểm kê (`GET /api/stocktakes`)
* **Response DTO:**
```csharp
public class StocktakeListResponseDto
{
    public Guid Id { get; set; }
    public string StocktakeNo { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? StartedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}
```

#### 2. Tạo đợt kiểm kê (`POST /api/stocktakes`)
* **Request DTO:**
```csharp
public class CreateStocktakeRequestDto
{
    [Required]
    [MaxLength(100)]
    public string StocktakeNo { get; set; } = null!;
    public Guid? ZoneId { get; set; }
    public List<Guid>? LocationIds { get; set; } // Phạm vi vị trí cụ thể (nếu có)
}
```

#### 3. Ghi nhận kết quả kiểm kê (`POST /api/stocktakes/{id}/count`)
* **Request DTO:**
```csharp
public class RecordCountRequestDto
{
    [Required]
    public Guid LocationId { get; set; }
    [Required]
    public Guid ItemId { get; set; }
    [Required]
    [MaxLength(100)]
    public string LotNo { get; set; } = null!;
    [Required]
    [Range(0, 9999999999)]
    public decimal CountedQty { get; set; }
}
```

#### 4. Phê duyệt chênh lệch kiểm kê (`POST /api/stocktakes/{id}/approve`)
* **Request DTO:**
```csharp
public class ApproveStocktakeRequestDto
{
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!; // Reason code cho sự chênh lệch (ví dụ: 'ADJ-COUNT')
    [MaxLength(500)]
    public string? Remarks { get; set; }
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
| Stocktake setup | Tạo phạm vi | Có loading, empty, error, filter, pagination và quyền theo action. |
| Counting screen | Scan location/Lot | Có loading, empty, error, filter, pagination và quyền theo action. |
| Variance approval | Duyệt lệch | Có loading, empty, error, filter, pagination và quyền theo action. |

### Các màn hình chính và thành phần UI

#### 1. Quản lý Đợt kiểm kê (`/admin/inventory/stocktakes`)
* Hiển thị danh sách các đợt kiểm kê: "Mã đợt", "Khu vực", "Trạng thái", "Giá trị chênh lệch (VNĐ)", "Ngày tạo", "Người tạo".
* Có nút **Tạo đợt kiểm kê** mở dialog, nút xem chi tiết đợt kiểm kê.

#### 2. Chi tiết và Thực thi kiểm kê (`/admin/inventory/stocktakes/{id}`)
* Hiển thị trạng thái đợt kiểm kê, thông tin khu vực/vị trí bị khóa.
* Đối với đợt ở trạng thái `Draft`: Có nút **Bắt đầu kiểm kê** (API sẽ tự động sinh `stocktake_items` dựa trên số dư tồn thực tế tại các vị trí, đồng thời chèn bản ghi khóa `location_locks` loại `ALL` để phong tỏa vị trí kệ).
* Đối với đợt ở trạng thái `Counting`:
  * Hiển thị danh sách các dòng kiểm kê: "Vị trí", "Vật tư", "Số lô", "Số tồn hệ thống", "Số lượng đếm", "Trạng thái".
  * Có nút **Nhập số lượng đếm** mở dialog nhập `CountedQty`.
  * Có nút **Gửi phê duyệt** (hoặc tự động tính chênh lệch khi hoàn tất đếm toàn bộ vị trí).
* Đối với đợt ở trạng thái chờ duyệt (`Pending_L1_Approve`, `Pending_L2_Approve`, `Pending_L3_Approve`):
  * Hiển thị bảng đối chiếu chênh lệch: highlight màu đỏ cho các dòng thiếu hụt (`variance_qty < 0`), màu xanh cho các dòng thừa (`variance_qty > 0`). Hiển thị tổng giá trị chênh lệch quy ra tiền mặt.
  * Hiển thị nút **Phê duyệt chênh lệch** dựa trên vai trò của User:
    * L1 Approve (nút hiện khi trạng thái là `Pending_L1_Approve` và User có quyền `Inventory.CycleCount.Approve.L1`).
    * L2 Approve (nút hiện khi trạng thái là `Pending_L2_Approve` và User có quyền `Inventory.CycleCount.Approve.L2`).
    * L3 Approve (nút hiện khi trạng thái là `Pending_L3_Approve` và User có quyền `Inventory.CycleCount.Approve.L3`).
    * Khi bấm duyệt, mở Dialog xác nhận chọn Reason Code và điền ghi chú.

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

1. **Tạo đợt kiểm kê:** User tạo đợt kiểm kê chọn phạm vi Zone hoặc danh sách các vị trí kiểm kê (`Draft`).
2. **Khóa vị trí & Chụp số tồn hệ thống:**
   - Khi nhấn "Bắt đầu kiểm kê", hệ thống lấy snapshot số dư tồn kho khả dụng hiện tại (`inventories`) tại các vị trí kệ thuộc phạm vi để điền vào `system_qty` trong `stocktake_items`.
   - Hệ thống tự động chèn các bản ghi khóa vị trí vào bảng `location_locks` (với `LockType = 'ALL'`, `Reason = 'Stocktake ' + StocktakeNo`) để phong tỏa kệ.
   - Chuyển trạng thái Đợt kiểm kê thành `Counting`.
3. **Nhập kết quả kiểm kê (Counting):**
   - Operator tiến hành đếm thực tế và nhập `counted_qty` cho từng dòng kiểm kê.
   - Trạng thái dòng kiểm kê chuyển sang `Counted`.
4. **Đối chiếu và Duyệt chênh lệch (Approve Flow):**
   - Khi đếm xong, hệ thống tự động quy đổi giá trị chênh lệch tài chính: `total_variance_amount = Sum(Abs(variance_qty) * standard_cost)`.
   - Hệ thống xác định cấp phê duyệt yêu cầu dựa trên giá trị tài chính chênh lệch:
     - Giá trị < 10 triệu VNĐ $\rightarrow$ Chuyển trạng thái sang `Pending_L1_Approve` (Yêu cầu quyền L1).
     - Giá trị từ 10 triệu đến 100 triệu VNĐ $\rightarrow$ Chuyển trạng thái sang `Pending_L2_Approve` (Yêu cầu quyền L2).
     - Giá trị > 100 triệu VNĐ $\rightarrow$ Chuyển trạng thái sang `Pending_L3_Approve` (Yêu cầu quyền L3).
   - Khi User có đủ thẩm quyền tương ứng tiến hành bấm **Phê duyệt**:
     - Bắt đầu DB Transaction để xử lý điều chỉnh tồn kho.
     - Tạo phiếu điều chỉnh tồn kho `stock_adjustments` ở trạng thái `Applied`.
     - Với mỗi dòng có chênh lệch `variance_qty != 0`:
       - Tạo dòng chi tiết `stock_adjustment_items` ghi nhận trước/sau/lệch.
       - Cập nhật trực tiếp số dư tồn kho trên bảng `inventories` (Thừa $\rightarrow$ cộng tồn & ghi ledger `ADJ_IN`; Thiếu $\rightarrow$ kiểm tra khả dụng, trừ tồn & ghi ledger `ADJ_OUT`).
     - Xóa toàn bộ các bản ghi khóa vị trí kệ trong bảng `location_locks`.
     - Chuyển trạng thái Đợt kiểm kê thành `Approved`.

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Location đang kiểm kê chặn move/pick
* Adjustment cần quyền
* Không sửa count sau duyệt

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Scan Lot lạ
* Variance vượt ngưỡng
* Location đang bận
* Approve thiếu quyền

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

* Audit stocktake
* Variance KPI
* Timeline location

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

* Count đúng
* Dư/thiếu
* Lock chặn giao dịch
* Approve atomic

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

* Kiểm kê cập nhật tồn đúng và có kiểm soát

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* ABC counting nâng cao

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





