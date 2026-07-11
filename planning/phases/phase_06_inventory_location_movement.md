# PHASE 06: Inventory by location & movement

## Execution spec maturity

- **Mức hiện tại:** 100% (Completed Spec & Implementation)
- **Đánh giá:** Hoàn tất thiết kế chi tiết cấu trúc Database schema PostgreSQL, API contracts chi tiết cho nghiệp vụ tồn kho và dịch chuyển kho, tích hợp liên module sử dụng interface IInventoryService dùng chung, giao diện UI/RF và các kịch bản lỗi chi tiết. Sẵn sàng thực thi.
- **Khi cần upgrade:** Upgrade nếu concurrency test phát hiện tranh chấp ghi phức tạp hơn dự kiến.

## 1. Mục tiêu

Quản lý tồn kho theo vị trí và chuyển vị trí an toàn, chống âm kho.

Phase này thuộc stage **MVP vận hành chắc** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Quản lý tồn kho theo vị trí và chuyển vị trí an toàn, chống âm kho.

### In scope

* Tạo module Inventory by location & movement
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

* Tạo module Inventory by location & movement
* Seed permission và reason code liên quan
* Cấu hình route/API/menu
* Chuẩn hóa DTO camelCase

### Cấu trúc module đề xuất

```text
backend/modules/inventory_location_movement/
frontend/features/inventory_location_movement/
planning/phases/phase_06_inventory_location_movement.md
```

### Permission seed đề xuất

* inventory_location_movement.read
* inventory_location_movement.create
* inventory_location_movement.update
* inventory_location_movement.approve
* inventory_location_movement.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

### Cấu trúc Schema chi tiết (PostgreSQL)

```sql
-- 1. Bảng lưu trữ số dư tồn kho theo Vị trí, Vật tư và Số lô
CREATE TABLE inventories (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    item_id uuid NOT NULL,
    lot_no character varying(100) NOT NULL,
    location_id uuid NOT NULL,
    qty_on_hand numeric(18,4) NOT NULL DEFAULT 0.0000,
    qty_reserved numeric(18,4) NOT NULL DEFAULT 0.0000,
    qty_available numeric(18,4) GENERATED ALWAYS AS (qty_on_hand - qty_reserved) STORED,
    row_version integer NOT NULL DEFAULT 1,
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_inventories" PRIMARY KEY (id),
    CONSTRAINT "CK_inventories_qty_on_hand" CHECK (qty_on_hand >= 0),
    CONSTRAINT "CK_inventories_qty_reserved" CHECK (qty_reserved >= 0 AND qty_reserved <= qty_on_hand)
);

-- Index duy nhất đảm bảo không trùng lặp dòng tồn kho
CREATE UNIQUE INDEX uq_inventories_tenant_item_lot_location 
ON inventories (tenant_id, item_id, lot_no, location_id);

-- 2. Bảng quản lý việc khóa/mở khóa vị trí ô kệ
CREATE TABLE location_locks (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    location_id uuid NOT NULL,
    lock_type character varying(50) NOT NULL, -- 'INBOUND', 'OUTBOUND', 'ALL'
    reason_code character varying(50) NOT NULL,
    locked_by character varying(100) NOT NULL,
    locked_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_location_locks" PRIMARY KEY (id)
);

CREATE UNIQUE INDEX uq_location_locks_tenant_location 
ON location_locks (tenant_id, location_id);

-- 3. Bảng ghi nhận yêu cầu dịch chuyển tồn kho nội bộ
CREATE TABLE inventory_movements (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    item_id uuid NOT NULL,
    lot_no character varying(100) NOT NULL,
    from_location_id uuid NOT NULL,
    to_location_id uuid NOT NULL,
    qty numeric(18,4) NOT NULL,
    status character varying(50) NOT NULL, -- 'Pending', 'Completed', 'Cancelled'
    reason_code character varying(50) NOT NULL,
    trace_id character varying(100),
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_inventory_movements" PRIMARY KEY (id),
    CONSTRAINT "CK_inventory_movements_qty" CHECK (qty > 0)
);

CREATE INDEX idx_inv_movements_tenant_status ON inventory_movements (tenant_id, status);

-- 4. Bảng nhật ký giao dịch thay đổi tồn kho (Immutable Ledger)
CREATE TABLE inventory_transactions (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    item_id uuid NOT NULL,
    lot_no character varying(100) NOT NULL,
    location_id uuid NOT NULL,
    transaction_type character varying(50) NOT NULL, -- 'RECEIVE', 'MOVE_OUT', 'MOVE_IN', 'ADJUST_ADD', 'ADJUST_SUB'
    qty numeric(18,4) NOT NULL,
    trace_id character varying(100),
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    CONSTRAINT "PK_inventory_transactions" PRIMARY KEY (id)
);

CREATE INDEX idx_inv_trans_tenant_lot_item ON inventory_transactions (tenant_id, lot_no, item_id);
```

### Chuẩn database áp dụng
* Bảng `inventory_transactions` là bất biến (Immutable Ledger). Không cho phép sửa đổi hay xóa bản ghi.
* Kiểu dữ liệu số lượng được chuẩn hóa thành `numeric(18,4)` để bảo toàn độ chính xác tài chính.

### Transaction boundary
* Mọi thao tác cập nhật tồn kho (tăng/giảm `qty_on_hand`, `qty_reserved`) và tạo bản ghi lịch sử `inventory_transactions` phải được bọc trong một Database Transaction duy nhất để bảo đảm tính toàn vẹn (Atomic).

---

## 6. Backend/API

### Tích hợp liên module (Cross-Module Integration)
Để module `Inbound` ghi nhận tồn kho khi nhận hàng mà không phụ thuộc trực tiếp vào DB Context của module `Inventory`, định nghĩa Interface dùng chung được đăng ký trong DI Container:

```csharp
namespace Nexustock.Modules.Inventory.Services;

public interface IInventoryService
{
    Task RecordReceiptAsync(
        Guid tenantId, 
        Guid itemId, 
        string lotNo, 
        Guid toLocationId, 
        decimal qty, 
        string username, 
        string traceId);
}
```

### API Contracts và DTOs

#### 1. Lấy số dư tồn kho (`GET /api/inventory/balances`)
* **Query Parameters:** `itemId` (Guid?), `locationId` (Guid?), `lotNo` (string?), `page` (int, default 1), `pageSize` (int, default 10).
* **Response DTO:**
```csharp
public class InventoryBalanceResponseDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string LotNo { get; set; } = null!;
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = null!;
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtyAvailable { get; set; }
}
```

#### 2. Yêu cầu chuyển vị trí (`POST /api/inventory/move`)
* **Request DTO:**
```csharp
public class MoveInventoryRequestDto
{
    [Required]
    public Guid ItemId { get; set; }
    [Required]
    [MaxLength(100)]
    public string LotNo { get; set; } = null!;
    [Required]
    public Guid FromLocationId { get; set; }
    [Required]
    public Guid ToLocationId { get; set; }
    [Required]
    [Range(0.0001, 9999999999)]
    public decimal Qty { get; set; }
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;
}
```

#### 3. Khóa vị trí ô kệ (`POST /api/locations/{id}/lock`)
* **Request DTO:**
```csharp
public class LockLocationRequestDto
{
    [Required]
    public string LockType { get; set; } = null!; -- 'INBOUND', 'OUTBOUND', 'ALL'
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;
}
```

#### 4. Mở khóa vị trí ô kệ (`POST /api/locations/{id}/unlock`)
* **Request:** Empty Body.

---

## 7. Frontend/RF/mobile

### Chuẩn UI và Giao diện Desktop-Native
* **Ngôn ngữ thiết kế:** Tuân thủ Fluent Design / WinUI 3 (Dark theme mặc định, bo góc mượt, spacing hợp lý, các nhãn hiển thị bắt buộc là Sentence case).
* Không sử dụng Inline Style. Tách CSS ra tệp riêng.

### Các màn hình chính

#### 1. Trang Quản lý tồn kho theo vị trí (`/admin/inventory/balances`)
* **Layout:** Một bảng hiển thị danh sách tồn kho gồm các cột: "Mã vật tư", "Tên vật tư", "Số lô", "Vị trí", "Tồn thực tế", "Đã giữ", "Khả dụng".
* **Chức năng:** Bộ lọc nhanh theo Vị trí, Mã vật tư, và trạng thái QC của số lô.

#### 2. Màn hình dịch chuyển tồn kho nội bộ (`/admin/inventory/move`)
* **RF/Handheld Mobile Layout:** Ưu tiên luồng thao tác quét mã vạch tuần tự:
  1. Quét số lô vật tư (Auto-focus trường "Số lô").
  2. Quét vị trí nguồn (Kiểm tra tồn tại số dư).
  3. Quét vị trí đích.
  4. Nhập số lượng và chọn lý do di chuyển (Reason code).
  5. Nút bấm "Xác nhận chuyển" (Sentence case).

#### 3. Màn hình Cấu hình Khóa vị trí (`/admin/inventory/locks`)
* **Layout:** Danh sách các vị trí ô kệ đang bị khóa kèm thông tin chi tiết: "Vị trí", "Kiểu khóa", "Lý do khóa", "Người khóa", "Thời gian khóa".
* **Hành động:** Nút "Mở khóa" nhanh đi kèm dialog xác nhận.

---

## 8. Execution flow

1. **Người dùng quét hoặc nhập Số lô + Mã vị trí nguồn:**
   - Hệ thống truy vấn số dư khả dụng (`qty_available`) của lô hàng tại vị trí đó.
2. **Người dùng quét vị trí đích:**
   - Hệ thống kiểm tra vị trí đích có đang bị khóa inbound (`LockType = 'INBOUND'` hoặc `'ALL'`) hay không.
3. **Nhập số lượng cần dịch chuyển và gửi yêu cầu:**
   - API xác thực các quy tắc nghiệp vụ (Validation & Business Rules).
4. **Thực thi trong Database Transaction:**
   - Khấu trừ `qty_on_hand` tại dòng tồn kho nguồn (nếu tồn kho khả dụng đủ và không có tranh chấp version).
   - Cộng thêm `qty_on_hand` tại dòng tồn kho đích (tạo mới dòng nếu chưa tồn tại).
   - Ghi nhận 2 bản ghi giao dịch bất biến vào `inventory_transactions`:
     - Bản ghi xuất: `transaction_type = 'MOVE_OUT'`, `qty = -qty`.
     - Bản ghi nhập: `transaction_type = 'MOVE_IN'`, `qty = qty`.
   - Cập nhật trạng thái phiếu chuyển `inventory_movements` thành `Completed`.
5. **Trả kết quả thành công và cập nhật lại giao diện.**

---

## 9. Validation & business rules

* **Không âm tồn:** Hệ thống tuyệt đối chặn mọi thao tác dịch chuyển có số lượng dịch chuyển lớn hơn số dư khả dụng hiện tại (`qty_available`).
* **Không dịch chuyển Lô hàng đang giữ (Hold):** Trước khi dịch chuyển, hệ thống phải liên kết kiểm tra bảng `lots` từ module Inbound/QC để đảm bảo `QcStatus = 'Release'`. Chặn dịch chuyển đối với Lot có status `'Hold'` hoặc `'Reject'`.
* **Kiểm tra trạng thái khóa vị trí (Location Lock Guard):** Chặn dịch chuyển đi từ vị trí bị khóa Outbound, và chặn dịch chuyển đến vị trí bị khóa Inbound.
* **Xác thực Optimistic Concurrency:** Trường `row_version` trong bảng `inventories` bắt buộc phải được truyền lên và so khớp để chống tranh chấp ghi đồng thời (Concurrent movement).
* **Chặn sức chứa vị trí (Capacity Guard):** Khi dịch chuyển hoặc nhận hàng, tổng lượng tồn kho thực tế (`qty_on_hand`) tại vị trí đích cộng thêm lượng mới dịch chuyển/nhập kho không được phép vượt quá sức chứa thiết lập (`MaxCapacity` của vị trí đó trong bảng `storage_locations`). Nếu vượt quá, chặn và trả lỗi `LOCATION_OVER_CAPACITY`.

---

## 10. Exception handling

### Các mã lỗi chuẩn của hệ thống (ErrorCode)

| Mã lỗi | Mô tả | Trạng thái HTTP |
|---|---|---|
| `INSUFFICIENT_QTY` | Số lượng dịch chuyển vượt quá tồn khả dụng | 400 Bad Request |
| `LOCATION_LOCKED` | Vị trí ô kệ đang bị khóa không cho phép thao tác | 400 Bad Request |
| `LOT_ON_HOLD` | Lô hàng đang bị giữ kiểm định chất lượng, không được di chuyển | 400 Bad Request |
| `CONCURRENCY_CONFLICT` | Dữ liệu tồn kho đã thay đổi bởi phiên làm việc khác | 409 Conflict |
| `INVALID_REASON_CODE` | Lý do di chuyển/khóa không hợp lệ | 400 Bad Request |
| `LOCATION_OVER_CAPACITY` | Số lượng vượt quá sức chứa tối đa của vị trí đích | 400 Bad Request |

### Nguyên tắc xử lý ngoại lệ
* Mọi lỗi xảy ra trong quá trình cập nhật số dư tồn kho bắt buộc phải rollback transaction để tránh sai lệch số dư thực tế và ledger.
* Mã lỗi và Trace ID phải được trả về giao diện đầy đủ phục vụ việc gỡ lỗi.

## 11. Observability

* Timeline Lot/Location
* Audit movement
* Inventory accuracy KPI

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

* Move đủ
* Move thiếu fail
* Concurrent move
* Lock chặn move

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

* Tồn theo vị trí khớp transaction sau mọi move

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* Slotting
* Task interleaving

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





