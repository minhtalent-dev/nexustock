# PHASE 07: Outbound picking & packing basic

## Execution spec maturity

- **Mức hiện tại:** 100% (Completed Spec & Details)
- **Đánh giá:** Hoàn tất thiết kế chi tiết cấu trúc Database schema PostgreSQL, API contracts chi tiết cho nghiệp vụ xuất kho cơ bản, sinh Pick Task theo FIFO/FEFO và trừ tồn kho thực tế kèm Ledger Transactions, cấu hình giao diện UI. Sẵn sàng thực thi.
- **Khi cần upgrade:** Upgrade nếu cần tích hợp các quy tắc chia Wave Picking phức tạp hơn.

## 1. Mục tiêu

Xuất kho cơ bản từ shipment đến picking, packing và trừ tồn kho an toàn, chống xuất âm.

Phase này thuộc stage **MVP vận hành chắc** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

### In scope

* Tạo các bảng dữ liệu xuất kho: `shipments`, `shipment_items`, `pick_tasks`, `packing_records`
* Seed permission và reason code liên quan đến xuất kho
* Cấu hình route/API/menu
* Chuẩn hóa DTO camelCase

### Non-negotiable output

* Cấu trúc Database schema chi tiết.
* API contracts và các DTOs cho nghiệp vụ Outbound.
* Giao diện UI quản lý xuất kho trên Web.
* Luồng E2E test tích hợp tự động hoàn tất 100%.

## 3. Điều kiện đầu vào

* Phase 06 (Inventory by location & movement) đã hoàn tất và test pass.
* Dữ liệu master data (Product, StorageLocation, Partner) đã sẵn sàng.
* Quyền hạn liên quan đã được cấu hình trong `Program.cs`.

## 4. Setup

### Cấu trúc module đề xuất

Đặt chung trong module Inventory hoặc tạo sub-folder trong module Inventory:
```text
backend/modules/Nexustock.Modules.Inventory/Entities/Shipment.cs
backend/modules/Nexustock.Modules.Inventory/Entities/ShipmentItem.cs
backend/modules/Nexustock.Modules.Inventory/Entities/PickTask.cs
backend/modules/Nexustock.Modules.Inventory/Entities/PackingRecord.cs
backend/modules/Nexustock.Modules.Inventory/Controllers/OutboundController.cs
```

### Permission seed đề xuất

* `Outbound.Shipments.View`
* `Outbound.Shipments.Create`
* `Outbound.Picks.Execute`
* `Outbound.Packing.Execute`

## 5. Database

### Cấu trúc Schema chi tiết (PostgreSQL)

```sql
-- 1. Bảng lưu trữ đơn xuất kho (Shipments)
CREATE TABLE shipments (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    shipment_no character varying(100) NOT NULL,
    partner_id uuid NOT NULL,
    status character varying(50) NOT NULL DEFAULT 'Open', -- 'Open', 'Allocated', 'Picking', 'Packed', 'Shipped', 'Cancelled'
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_shipments" PRIMARY KEY (id)
);

CREATE UNIQUE INDEX uq_shipments_tenant_no ON shipments (tenant_id, shipment_no);

-- 2. Bảng chi tiết dòng đơn xuất kho (ShipmentItems)
CREATE TABLE shipment_items (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    shipment_id uuid NOT NULL,
    item_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    requested_qty numeric(18,4) NOT NULL DEFAULT 0.0000,
    picked_qty numeric(18,4) NOT NULL DEFAULT 0.0000,
    packed_qty numeric(18,4) NOT NULL DEFAULT 0.0000,
    CONSTRAINT "PK_shipment_items" PRIMARY KEY (id),
    CONSTRAINT "FK_shipment_items_shipments" FOREIGN KEY (shipment_id) REFERENCES shipments(id) ON DELETE CASCADE,
    CONSTRAINT "CK_shipment_items_requested" CHECK (requested_qty > 0),
    CONSTRAINT "CK_shipment_items_picked" CHECK (picked_qty >= 0 AND picked_qty <= requested_qty),
    CONSTRAINT "CK_shipment_items_packed" CHECK (packed_qty >= 0 AND packed_qty <= picked_qty)
);

CREATE UNIQUE INDEX uq_shipment_items_tenant_shipment_item ON shipment_items (tenant_id, shipment_id, item_id);

-- 3. Bảng nhiệm vụ lấy hàng (PickTasks)
CREATE TABLE pick_tasks (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    shipment_id uuid NOT NULL,
    item_id uuid NOT NULL,
    lot_no character varying(100) NOT NULL,
    from_location_id uuid NOT NULL,
    qty numeric(18,4) NOT NULL,
    picked_qty numeric(18,4) NOT NULL DEFAULT 0.0000,
    status character varying(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Picking', 'Completed', 'Cancelled'
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_pick_tasks" PRIMARY KEY (id),
    CONSTRAINT "FK_pick_tasks_shipments" FOREIGN KEY (shipment_id) REFERENCES shipments(id) ON DELETE CASCADE,
    CONSTRAINT "CK_pick_tasks_qty" CHECK (qty > 0),
    CONSTRAINT "CK_pick_tasks_picked" CHECK (picked_qty >= 0 AND picked_qty <= qty)
);

CREATE INDEX idx_pick_tasks_tenant_shipment ON pick_tasks (tenant_id, shipment_id);

-- 4. Bảng ghi nhận đóng gói (PackingRecords)
CREATE TABLE packing_records (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    shipment_id uuid NOT NULL,
    package_no character varying(100) NOT NULL,
    weight numeric(18,4) NOT NULL DEFAULT 0.0000,
    status character varying(50) NOT NULL DEFAULT 'Open', -- 'Open', 'Completed', 'Cancelled'
    created_at timestamp with time zone NOT NULL,
    created_by character varying(100) NOT NULL,
    updated_at timestamp with time zone,
    updated_by character varying(100),
    CONSTRAINT "PK_packing_records" PRIMARY KEY (id),
    CONSTRAINT "FK_packing_records_shipments" FOREIGN KEY (shipment_id) REFERENCES shipments(id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX uq_packing_records_tenant_package ON packing_records (tenant_id, package_no);
```

### Chuẩn database áp dụng

* Mọi bảng nghiệp vụ có `id`, `tenantId`, `createdAt`, `createdBy`, `updatedAt`, `updatedBy` nếu có chỉnh sửa.
* Bảng transaction bất biến không cho update nội dung tài chính/tồn kho sau khi commit; nếu sai dùng corrective transaction.
* Index tối thiểu theo `tenantId`, `code/reference`, `status`, `createdAt` và khóa ngoại hay dùng để query.
* Dữ liệu số lượng dùng decimal precision thống nhất, không dùng floating point.
* Status lưu bằng enum/string ổn định, không lưu text tự do.
* Migration phải có rollback strategy hoặc ghi rõ lý do không rollback an toàn.

### Transaction boundary

* Mọi thay đổi inventory hoặc trạng thái quan trạng phải nằm trong một transaction.
* Không gọi hệ thống ngoài trong DB transaction dài.
* Nếu cần publish event, dùng outbox/integration log sau commit.
* Chống double-submit bằng idempotency key ở command quan trọng.

---

## 6. Backend/API

### API Contracts và DTOs

#### 1. Lấy danh sách đơn xuất (`GET /api/outbound/shipments`)
* **Response DTO:**
```csharp
public class ShipmentListResponseDto
{
    public Guid Id { get; set; }
    public string ShipmentNo { get; set; } = null!;
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}
```

#### 2. Tạo đơn xuất (`POST /api/outbound/shipments`)
* **Request DTO:**
```csharp
public class CreateShipmentRequestDto
{
    [Required]
    [MaxLength(100)]
    public string ShipmentNo { get; set; } = null!;
    [Required]
    public Guid PartnerId { get; set; }
    [Required]
    public List<CreateShipmentItemDto> Items { get; set; } = null!;
}

public class CreateShipmentItemDto
{
    [Required]
    public Guid ItemId { get; set; }
    [Required]
    public Guid UomId { get; set; }
    [Required]
    [Range(0.0001, 9999999999)]
    public decimal RequestedQty { get; set; }
}
```

#### 3. Sinh Pick Tasks (`POST /api/outbound/shipments/{id}/generate-picks`)
* Sinh Pick Tasks tự động dựa trên nguyên tắc **FIFO/FEFO** và tăng `qty_reserved` của các dòng tồn kho tương ứng trong bảng `inventories`.

#### 4. Hoàn tất Pick Task (`POST /api/outbound/picks/{id}/complete`)
* **Request DTO:**
```csharp
public class CompletePickRequestDto
{
    [Required]
    [Range(0.0001, 9999999999)]
    public decimal PickedQty { get; set; }
}
```

#### 5. Hoàn tất đóng gói đơn xuất (`POST /api/outbound/packing/{shipmentId}/complete`)
* **Request DTO:**
```csharp
public class CompletePackingRequestDto
{
    [Required]
    [MaxLength(100)]
    public string PackageNo { get; set; } = null!;
    [Required]
    [Range(0.0001, 9999999999)]
    public decimal Weight { get; set; }
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

---

## 7. Frontend/RF/mobile

### Các màn hình chính

#### 1. Quản lý Đơn xuất kho (`/admin/outbound`)
* Hiển thị danh sách đơn xuất kho gồm các cột: "Mã đơn", "Đối tác", "Trạng thái", "Ngày tạo", "Người tạo".
* Có nút **Tạo đơn xuất**, **Sinh Pick** và mở rộng xem chi tiết đơn hàng (các dòng hàng hóa, Pick Tasks, Packing Records).

#### 2. Giao diện thực thi Picking & Packing
* Tích hợp dialog thực thi Picking cho Operator (nhập số lượng đã pick).
* Dialog đóng gói đơn hàng (nhập số kiện hàng và cân nặng).

### Chuẩn UI áp dụng

* UI text dùng Sentence case.
* Không dùng inline style.
* Sử dụng Next.js, Tailwind CSS và Shadcn UI.
* Mọi action nguy hiểm có confirm rõ ràng.
* Mọi màn hình có loading, empty, error, unauthorized state.
* Bảng dữ liệu có filter, pagination và trạng thái no result.
* RF/mobile ưu tiên input scan auto-focus, font lớn, ít nút, phản hồi rõ.

### State cần hiển thị

* Draft/open/in progress/completed/cancelled nếu phase có workflow.
* Locked/blocked/exception nếu thao tác bị chặn.
* Last updated và actor cho dữ liệu quan trọng.
* Trace ID hoặc reference ID khi cần hỗ trợ vận hành.

---

## 8. Execution flow

1. **Tạo đơn xuất kho:** User tạo đơn xuất với các dòng hàng hóa yêu cầu.
2. **Sinh nhiệm vụ lấy hàng (Generate Picks):**
   - Hệ thống tự động tìm số dư tồn kho khả dụng (`qty_available > 0`) trong kho theo nguyên tắc FIFO (hoặc FEFO nếu lô hàng có ngày hết hạn).
   - Chỉ chọn các lô hàng có trạng thái QC là `Release`.
   - Tạo các bản ghi `pick_tasks` ở trạng thái `Pending`.
   - Cập nhật tăng `qty_reserved` trên bảng `inventories` tương ứng để giữ hàng.
   - Chuyển trạng thái Đơn hàng sang `Allocated`.
3. **Thực hiện lấy hàng (Complete Pick):**
   - Operator lấy hàng thực tế tại kệ và xác nhận số lượng.
   - Hệ thống trừ `qty_on_hand` và `qty_reserved` tại dòng tồn kho nguồn. Nếu tồn kho bằng 0, xóa dòng tồn kho.
   - Tạo transaction ledger `PICK_OUT` (âm) trong bảng `inventory_transactions`.
   - Cập nhật `picked_qty` của `shipment_items` và `pick_tasks`.
4. **Đóng gói (Packing):**
   - Đóng gói hàng đã pick vào kiện, nhập mã kiện `package_no` và cân nặng `weight`.
   - Cập nhật đơn hàng thành `Packed` (hoặc `Shipped` nếu hoàn thành xuất kho).

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

---

## 9. Validation & business rules

* **Chặn lô hàng QC Hold:** Không được phân bổ hoặc sinh Pick Task từ lô hàng có `QcStatus != 'Release'`.
* **Chặn xuất âm:** Không cho phép pick quá tồn kho khả dụng thực tế.
* **Chặn vị trí bị khóa Outbound:** Chặn sinh pick task từ các vị trí đang bị khóa Outbound hoặc ALL.

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

---

## 10. Exception handling

### Các mã lỗi chuẩn

| Mã lỗi | Mô tả | HTTP Status |
|---|---|---|
| `SHIPMENT_NOT_FOUND` | Không tìm thấy đơn xuất | 404 Not Found |
| `INVALID_SHIPMENT_STATUS` | Trạng thái đơn xuất không hợp lệ để thực hiện thao tác | 400 Bad Request |
| `LOT_NOT_RELEASED` | Lô hàng chưa được giải phóng QC, không thể phân bổ | 400 Bad Request |
| `INSUFFICIENT_INVENTORY` | Không đủ tồn kho khả dụng để phân bổ | 400 Bad Request |
| `PICK_TASK_NOT_FOUND` | Không tìm thấy nhiệm vụ pick | 404 Not Found |
| `PICK_QTY_EXCEEDED` | Số lượng pick thực tế vượt quá yêu cầu | 400 Bad Request |

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

---

## 11. Observability

* Audit pick/pack/ship
* Timeline shipment
* KPI pick productivity

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

---

## 12. Test plan

* FIFO
* FEFO
* Short pick
* Pack mismatch
* Concurrent ship

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

---

## 13. Acceptance criteria

* Đơn xuất end-to-end chạy đúng và tồn giảm chính xác

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* Wave picking
* Carrier integration

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
* If API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.
