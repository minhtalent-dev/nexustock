# PHASE 15: LPN pallet management

## Execution spec maturity

- **Mức hiện tại:** ✅ Hoàn thành Triển khai (100% Completed)
- **Đánh giá:** Đã hoàn thiện toàn bộ mã nguồn Lpn Module cho backend và giao diện quản lý trên frontend Web/Mobile. Đã chạy thử nghiệm tích hợp thành công 100% qua kịch bản kiểm thử tự động `verify_lpn.ps1` và xác thực thủ công thành công qua browser subagent.
- **Khi cần upgrade:** Upgrade nếu cần nested LPN, split/merge pallet hoặc audit theo container nhiều tầng.

## 1. Mục tiêu

Quản lý Pallet/LPN để gom Lot và di chuyển hàng loạt bằng một mã.

Phase này thuộc stage **Advanced WMS** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Quản lý Pallet/LPN để gom Lot và di chuyển hàng loạt bằng một mã.

### In scope

* Tạo module LPN pallet management
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

* Tạo module LPN pallet management
* Seed permission/rule liên quan
* Cập nhật menu và route

### Cấu trúc module đề xuất

```text
backend/modules/lpn_pallet_management/
frontend/features/lpn_pallet_management/
planning/phases/phase_15_lpn_pallet_management.md
```

### Permission seed đề xuất

* lpn_pallet_management.read
* lpn_pallet_management.create
* lpn_pallet_management.update
* lpn_pallet_management.approve
* lpn_pallet_management.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `Lpns` | Đơn vị logistics | LpnNo,status,location |
| `LpnItems` | Lot trong LPN | LotId,qty |
| `LpnEvents` | Timeline LPN | Attach,detach,move,ship |

#### Cấu trúc bảng SQL chi tiết cho PostgreSQL:

```sql
-- 1. Bảng quản lý Pallet/LPN
CREATE TABLE lpns (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    lpn_no VARCHAR(100) NOT NULL,
    location_id UUID NOT NULL,                     -- Vị trí hiện tại của LPN
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',  -- ACTIVE, SHIPPED, EMPTY
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    row_version INT NOT NULL DEFAULT 1             -- Optimistic Concurrency Token
);

CREATE UNIQUE INDEX uq_lpns_tenant_lpn ON lpns(tenant_id, lpn_no);
CREATE INDEX idx_lpns_tenant_location ON lpns(tenant_id, location_id);

-- 2. Bảng ghi nhận lịch sử dịch chuyển / thao tác trên LPN
CREATE TABLE lpn_events (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    lpn_id UUID NOT NULL REFERENCES lpns(id) ON DELETE CASCADE,
    event_type VARCHAR(50) NOT NULL,               -- CREATE, ATTACH, DETACH, MOVE, SHIP, EMPTY
    item_id UUID,
    lot_no VARCHAR(100),
    qty DECIMAL(18,6),
    from_location_id UUID,
    to_location_id UUID,
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL
);

CREATE INDEX idx_lpn_events_tenant_lpn ON lpn_events(tenant_id, lpn_id);

-- 3. Nâng cấp bảng inventories hiện có (Chạy qua Migration của module Inventory)
ALTER TABLE inventories ADD COLUMN lpn_id UUID NULL REFERENCES lpns(id) ON DELETE SET NULL;
CREATE INDEX idx_inventories_tenant_lpn ON inventories(tenant_id, lpn_id);
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

| API | Mục đích | Ghi chú triển khai |
|---|---|---|
| `POST /api/lpns` | Tạo LPN | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/lpns/{id}/items` | Gán Lot | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/lpns/{id}/move` | Move LPN | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/lpns/{id}/close` | Đóng LPN | Có auth, validation, trace ID và response lỗi chuẩn. |

#### Chi tiết các endpoint API (JSON camelCase):

* **POST /api/lpns** (Tạo LPN trống):
  - Request: `{ "lpnNo": "LPN-20260714-001", "locationId": "00000000-0000-0000-0000-000000000041" }`
  - Response: `{ "id": "e3b4c5d6-1234-5678-abcd-ef0123456789", "lpnNo": "LPN-20260714-001", "locationId": "00000000-0000-0000-0000-000000000041", "status": "ACTIVE" }`

* **POST /api/lpns/{id}/attach** (Đóng hàng vào LPN):
  - Request: `{ "itemId": "f8e8f296-f0ab-4fac-adae-7ecdfe5b268e", "lotNo": "LOT-REP-E2E-001", "qty": 50.0 }`
  - Response: `{ "success": true, "message": "Đã đóng 50.0 sản phẩm vào LPN thành công." }`

* **POST /api/lpns/{id}/detach** (Rút hàng khỏi LPN):
  - Request: `{ "itemId": "f8e8f296-f0ab-4fac-adae-7ecdfe5b268e", "lotNo": "LOT-REP-E2E-001", "qty": 20.0 }`
  - Response: `{ "success": true, "message": "Đã rút 20.0 sản phẩm khỏi LPN thành công." }`

* **POST /api/lpns/{id}/move** (Di chuyển pallet LPN):
  - Request: `{ "targetLocationId": "00000000-0000-0000-0000-000000000042" }`
  - Response: `{ "success": true, "message": "LPN đã được dịch chuyển thành công sang vị trí mới." }`

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
| LPN builder | Gom Lot | Có loading, empty, error, filter, pagination và quyền theo action. |
| LPN move scan | Scan LPN và vị trí | Có loading, empty, error, filter, pagination và quyền theo action. |
| LPN detail | Timeline | Có loading, empty, error, filter, pagination và quyền theo action. |

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

1. Tạo LPN
2. Scan Lot
3. Attach qty
4. Scan target
5. Move atomic
6. Close/ship

#### Chi tiết Thuật toán & Mã nguồn giả lập (C#):

##### 8.1 Thuật toán Đóng hàng vào LPN (Attach)
Khi người dùng yêu cầu gán số lượng hàng hóa vào LPN:
1. **Kiểm tra trạng thái LPN**: Lock pessimistic dòng LPN cần xử lý. Đảm bảo LPN ở trạng thái `ACTIVE` và vị trí hiện tại của LPN khớp với vị trí của dòng tồn kho nguồn.
2. **Kiểm tra tồn kho**: Tìm dòng tồn kho (`inventories`) thỏa mãn `itemId`, `lot_no`, `location_id` và `lpn_id IS NULL`. Lock pessimistic dòng này.
3. **Thực hiện tách dòng tồn kho (Split Row)**:
   - Nếu số lượng yêu cầu gán `qty < inventories.qty_on_hand`:
     - Giảm `qty_on_hand` của dòng tồn kho ban đầu đi `qty`.
     - Tạo một dòng tồn kho mới trong DB với `lpn_id = LpnId`, `qty_on_hand = qty` và copy toàn bộ thông tin Lot, Location từ dòng cũ.
   - Nếu số lượng yêu cầu gán bằng đúng tồn kho hiện tại `qty == inventories.qty_on_hand`:
     - Cập nhật dòng tồn kho hiện tại: Gán `lpn_id = LpnId`.
4. **Ghi nhận sự kiện**: Thêm bản ghi vào `lpn_events` với `event_type = 'ATTACH'`.

##### 8.2 Thuật toán Di chuyển LPN (Move Atomic)
Di chuyển toàn bộ pallet sang vị trí kệ mới:
1. **Lock dữ liệu nguồn**: Bắt đầu Transaction. Lock dòng LPN trong database theo ID.
2. **Kiểm tra dung lượng kệ đích**: Gọi Capacity Guard trên kệ đích để đảm bảo có thể chứa toàn bộ trọng lượng/thể tích của LPN.
3. **Cập nhật vị trí LPN**: Đổi `location_id` của LPN sang vị trí đích.
4. **Cập nhật vị trí tồn kho**: 
   - Tìm toàn bộ dòng `inventories` có `lpn_id = LpnId`.
   - Cập nhật đồng loạt: `location_id = targetLocationId`.
5. **Ghi nhận Timeline**:
   - Ghi nhận `LpnEvent` loại `MOVE` lưu vết vị trí cũ và mới.
   - Tạo các dòng `InventoryMovement` loại `LPN_MOVE` cho từng mặt hàng trên LPN để lưu vết lịch sử dịch chuyển tồn kho.

##### 8.3 C# Application Service Implementation (Pseudo-code)

```csharp
public class LpnService : ILpnService
{
    private readonly LpnDbContext _dbContext;
    private readonly IInventoryContext _inventoryContext; // Để thao tác cập nhật inventories

    public LpnService(LpnDbContext dbContext, IInventoryContext inventoryContext)
    {
        _dbContext = dbContext;
        _inventoryContext = inventoryContext;
    }

    public async Task<bool> AttachToLpnAsync(Guid tenantId, Guid lpnId, AttachLpnDto dto, string operatorName)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            // 1. Lock và check LPN
            var lpn = await _dbContext.Lpns
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == lpnId);
            if (lpn == null || lpn.Status != "ACTIVE")
                throw new Exception("LPN không tồn tại hoặc đã bị đóng khóa.");

            // 2. Tìm dòng tồn kho tự do (chưa thuộc LPN nào) tại vị trí của LPN
            var sourceInv = await _inventoryContext.Inventories
                .FirstOrDefaultAsync(i => i.TenantId == tenantId 
                                       && i.LocationId == lpn.LocationId 
                                       && i.ItemId == dto.ItemId 
                                       && i.LotNo == dto.LotNo 
                                       && i.LpnId == null);
            if (sourceInv == null || sourceInv.QtyAvailable < dto.Qty)
                throw new Exception("Không đủ tồn kho tự do tại vị trí để đóng vào LPN.");

            // 3. Tách dòng tồn kho (Split Row)
            if (sourceInv.QtyOnHand > dto.Qty)
            {
                // Tách một phần
                sourceInv.QtyOnHand -= dto.Qty;
                
                var newInv = new Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = dto.ItemId,
                    LotNo = dto.LotNo,
                    LocationId = lpn.LocationId,
                    LpnId = lpn.Id,
                    QtyOnHand = dto.Qty,
                    QtyReserved = 0,
                    CreatedBy = operatorName,
                    CreatedAt = DateTime.UtcNow
                };
                await _inventoryContext.Inventories.AddAsync(newInv);
            }
            else
            {
                // Gán toàn bộ dòng
                sourceInv.LpnId = lpn.Id;
            }

            // 4. Lưu vết sự kiện LPN
            var lpnEvent = new LpnEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LpnId = lpn.Id,
                EventType = "ATTACH",
                ItemId = dto.ItemId,
                LotNo = dto.LotNo,
                Qty = dto.Qty,
                FromLocationId = lpn.LocationId,
                ToLocationId = lpn.LocationId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = operatorName
            };
            await _dbContext.LpnEvents.AddAsync(lpnEvent);

            await _dbContext.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Không gán Lot hold
* Một Lot không thuộc hai LPN active cùng qty
* Move atomic toàn LPN

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Duplicate attach
* LPN closed
* Location locked

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

* Timeline LPN
* Audit attach/detach/move

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

* Attach
* Move atomic
* Closed lock
* Partial detach

#### Kịch bản kiểm thử chi tiết (Test Cases):

* **TC-01 (Create LPN)**: Tạo LPN trống `LPN-TEST-001` tại kệ `LOC-A-01`. Kiểm tra bản ghi sinh ra đúng trạng thái `ACTIVE`.
* **TC-02 (Attach Partial Qty - Split Row)**: Kệ `LOC-A-01` có 100 sản phẩm tự do. Thực hiện đóng 40 sản phẩm vào LPN. Xác nhận trong DB:
  - Xuất hiện 1 dòng tồn kho mới có `LpnId = LPN-TEST-001` và `QtyOnHand = 40`.
  - Dòng tồn kho tự do ban đầu giảm xuống còn `QtyOnHand = 60` và `LpnId = NULL`.
* **TC-03 (Move Pallet Atomic)**: Thực hiện di chuyển LPN `LPN-TEST-001` từ kệ `LOC-A-01` sang `LOC-A-02`. Kiểm tra:
  - Bản ghi `lpns` đổi vị trí sang `LOC-A-02`.
  - Toàn bộ các dòng `inventories` có `LpnId = LPN-TEST-001` tự động cập nhật `location_id` sang `LOC-A-02`.
  - Ghi nhận `LpnEvent` loại `MOVE`.
* **TC-04 (Detach Qty)**: Rút 10 sản phẩm từ LPN. Kiểm tra số lượng tồn tự do tăng lên 10, số lượng trên LPN giảm xuống tương ứng.

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

* Một scan LPN di chuyển đúng toàn bộ hàng
* **AC-01 (Tạo và Quản lý LPN)**: Cho phép tạo LPN, theo dõi trạng thái và lịch sử hoạt động chính xác.
* **AC-02 (Đóng/Rút hàng chính xác)**: Hỗ trợ đóng gói hàng hóa vào LPN và chia tách dòng tồn kho tự động không gây thất thoát hoặc sai lệch số dư.
* **AC-03 (Di chuyển nguyên Pallet)**: Hệ thống tự động dịch chuyển đồng loạt mọi mặt hàng nằm trong LPN sang vị trí mới chỉ bằng 1 thao tác quét mã LPN và mã kệ đích trên Handheld.

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* Container nesting

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





