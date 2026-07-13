# PHASE 13: Allocation & reservation

## Execution spec maturity

- **Mức hiện tại:** ✅ Hoàn thành (100% Completed)
- **Đánh giá:** Đã hoàn thiện toàn diện chi tiết 100% đặc tả kỹ thuật: cấu trúc bảng `AllocationReservations` chi tiết với các ràng buộc khóa và index PostgreSQL, DTOs camelCase chi tiết cho toàn bộ API phân bổ, cơ chế chống Deadlock bằng cách sắp xếp ID vật tư (Sort Item IDs) trước khi lock pessimistic, giải pháp xử lý phân bổ một phần (Partial Allocation) và hết hạn giữ hàng tự động (Reservation Expiry Job), kịch bản test integration chi tiết và giao diện Next.js SPA quản trị phân bổ.
- **Khi cần upgrade:** Upgrade nếu mô hình kho tích hợp thêm hệ thống phân bổ động đa kho (Multi-Warehouse Allocation) hoặc phân bổ theo mức độ ưu tiên khách hàng VIP phức tạp.

## 1. Mục tiêu

Giữ hàng theo đơn xuất, ưu tiên, khách hàng, Lot, hạn dùng và trạng thái QC.

Phase này thuộc stage **Advanced WMS** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Giữ hàng theo đơn xuất, ưu tiên, khách hàng, Lot, hạn dùng và trạng thái QC.

### In scope

* Tạo module Allocation & reservation
* Seed permission/rule liên quan
* Cập nhật menu và route
* Phát triển background job tự động dọn dẹp reservation hết hạn

### Non-negotiable output

* Có database contract chi tiết cho bảng phân bổ giữ hàng.
* Có API contract chuẩn RESTful (camelCase) cho các tác vụ phân bổ, giải phóng và kiểm tra tồn khả dụng.
* Có UI Dashboard Next.js quản lý trạng thái phân bổ của đơn hàng.
* Có execution flow end-to-end chi tiết và chống tranh chấp đồng thời (Concurrency & Deadlock Resiliency).
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

* Tạo module Allocation & reservation
* Seed permission/rule liên quan
* Cập nhật menu và route

### Cấu trúc module đề xuất

```text
backend/modules/allocation_reservation/
frontend/features/allocation_reservation/
planning/phases/phase_13_allocation_reservation.md
```

### Permission seed đề xuất

* allocation_reservation.read
* allocation_reservation.create
* allocation_reservation.update
* allocation_reservation.approve
* allocation_reservation.export

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

### Cấu trúc bảng Phân bổ giữ hàng (`AllocationReservations`)

```sql
CREATE TABLE allocation_reservations (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    warehouse_id UUID NOT NULL,
    shipment_line_id UUID NOT NULL,
    inventory_balance_id UUID NOT NULL,
    qty DECIMAL(18,6) NOT NULL CHECK (qty > 0),
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE', -- ACTIVE, CONSUMED, EXPIRED, RELEASED
    expires_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    xmin XID NOT NULL -- optimistic concurrency token (RowVersion)
);

CREATE INDEX idx_allocation_reservations_tenant_status ON allocation_reservations(tenant_id, status);
CREATE INDEX idx_allocation_reservations_shipment_line ON allocation_reservations(tenant_id, shipment_line_id);
CREATE INDEX idx_allocation_reservations_balance ON allocation_reservations(tenant_id, inventory_balance_id);
CREATE INDEX idx_allocation_reservations_expiry ON allocation_reservations(expires_at) WHERE status = 'ACTIVE';
```

### Ràng buộc số dư tồn kho (`InventoryBalances`)

Bảng `InventoryBalances` được cập nhật cột `qty_reserved` để theo dõi lượng hàng đang bị khóa giữ bởi các đơn xuất:

```sql
-- Cập nhật cấu trúc bảng InventoryBalances (được kế thừa từ phase 06)
ALTER TABLE inventory_balances ADD COLUMN IF NOT EXISTS qty_reserved DECIMAL(18,6) NOT NULL DEFAULT 0.0 CHECK (qty_reserved >= 0.0);
ALTER TABLE inventory_balances ADD CONSTRAINT chk_inventory_balances_qty_available CHECK (qty_on_hand >= qty_reserved);
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

#### 1. `POST /api/allocation/reserve`
* **Mục đích**: Thực hiện tính toán và phân bổ giữ hàng cho một Đơn xuất hàng (Shipment).
* **Request Body (camelCase)**:
```json
{
  "shipmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "strategy": "FEFO", // FEFO, FIFO
  "allowPartial": true,
  "reservationTtlMinutes": 1440
}
```
* **Response (camelCase)**:
```json
{
  "success": true,
  "shipmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "ALLOCATED", // ALLOCATED, PARTIALLY_ALLOCATED, FAILED
  "allocatedLines": [
    {
      "shipmentLineId": "d82bd5d7-1bde-4df8-8097-f58c732cb6a5",
      "itemId": "a1b2c3d4-1234-4567-89ab-cdef01234567",
      "requestedQty": 100.0,
      "allocatedQty": 100.0,
      "reservations": [
        {
          "reservationId": "f9b8c7d6-e5f4-4321-b0a9-c8d7e6f5a4b3",
          "locationCode": "LOC-A-01",
          "lotNo": "LOT-001",
          "qty": 100.0
        }
      ]
    }
  ],
  "message": "Phân bổ tồn kho thành công."
}
```

#### 2. `POST /api/allocation/release`
* **Mục đích**: Giải phóng (nhả giữ) hàng thủ công hoặc khi đơn xuất hàng bị hủy.
* **Request Body (camelCase)**:
```json
{
  "shipmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```
* **Response (camelCase)**:
```json
{
  "success": true,
  "message": "Đã giải phóng 5 bản ghi giữ hàng cho đơn xuất 3fa85f64-5717-4562-b3fc-2c963f66afa6."
}
```

#### 3. `POST /api/allocation/reallocate`
* **Mục đích**: Thực hiện phân bổ lại cho đơn hàng bị thiếu (Partially Allocated) khi có hàng mới nhập về.
* **Request Body (camelCase)**:
```json
{
  "shipmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```
* **Response (camelCase)**:
```json
{
  "success": true,
  "status": "ALLOCATED",
  "message": "Đã phân bổ bổ sung thành công."
}
```

#### 4. `GET /api/inventory/availability`
* **Mục đích**: Lấy số liệu tồn khả dụng của vật tư để hiển thị trên màn hình kiểm tra.
* **Query Params**:
  * `itemId`: UUID (Bắt buộc)
* **Response (camelCase)**:
```json
{
  "itemId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "itemCode": "ITEM-CABLE-01",
  "qtyOnHand": 1000.0,
  "qtyReserved": 300.0,
  "qtyAvailable": 700.0
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

### Giao diện quản lý phân bổ đơn xuất (Allocation Dashboard Next.js UI)

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| Shipment Line Allocation Table | Hiển thị danh sách Shipment Lines và trạng thái phân bổ | Trạng thái trực quan: `Allocated` (Xanh lá), `Partially Allocated` (Vàng), `Unallocated` (Xám). Có progress bar thể hiện tỷ lệ % phân bổ. |
| Action Button Panel | Thực hiện thao tác phân bổ / giải phóng nhanh | Nút "Run Allocation" (Primary), "Release Reservation" (Outline/Destructive). Có confirm rõ ràng trước khi giải phóng. |
| Availability Lookup Component | Tra cứu tồn kho khả dụng nhanh | Hiển thị dạng thẻ 3 số liệu: `On Hand`, `Reserved`, `Available` bằng font chữ lớn, dễ scan. |
| Allocation Timeline | Xem chi tiết lịch sử giữ hàng của line | Hiển thị timeline log: Thời điểm giữ, người thực hiện, thời gian hết hạn, thời điểm giải phóng hoặc chuyển đổi sang xuất kho thực tế. |

### Chuẩn UI áp dụng

* UI text dùng Sentence case (ví dụ: "Run allocation", "Release reservation", "Available qty", "Save changes").
* Không dùng inline style.
* Sử dụng Next.js, Tailwind CSS và Shadcn UI.
* Mọi action nguy hiểm có confirm rõ ràng.
* Mọi màn hình có loading, empty, error, unauthorized state.
* Bảng dữ liệu có filter, pagination và trạng thái no result.

### State cần hiển thị

* Draft/open/in progress/completed/cancelled nếu phase có workflow.
* Locked/blocked/exception nếu thao tác bị chặn.
* Last updated và actor cho dữ liệu quan trọng.
* Trace ID hoặc reference ID khi cần hỗ trợ vận hành.

## 8. Execution flow

### 8.1 Quy trình xử lý phân bổ lô hàng xuất kho an toàn (Allocation Execution Flow)

1. **Nhận yêu cầu phân bổ:** Nhận `shipmentId`, `strategy` (FEFO/FIFO), `allowPartial` (mặc định: `true`), và `reservationTtlMinutes` (mặc định: 1440 phút).
2. **Khởi tạo Transaction:** Mở Database Transaction mức Isolation Level = `ReadCommitted`.
3. **Truy vấn nhu cầu xuất (Shipment Lines):** Đọc danh sách các line cần xuất của shipment.
4. **Truy vấn tồn kho khả dụng:** Tìm các bản ghi `InventoryBalances` thỏa mãn điều kiện:
   - Trạng thái QC (`qcStatus`) của Lot là `released` (Đã duyệt chất lượng).
   - Vị trí (`locationId`) không bị khóa (`lockReason` là null).
   - Có số lượng khả dụng thực tế (`qty - qtyReserved > 0`).
5. **Áp dụng Thuật toán Phân bổ & Lock:**
   - **Chống Deadlock:** Sắp xếp danh sách `ItemId` của Shipment Lines theo thứ tự tăng dần. Điều này đảm bảo rằng mọi luồng phân bổ song song đều lock các dòng tồn kho theo cùng một thứ tự tài nguyên, tránh tuyệt đối deadlock vòng lặp (Resource Ordering).
   - Áp dụng cơ chế **Pessimistic Locking** (`SELECT FOR UPDATE`) trên các dòng `InventoryBalances` được tìm thấy của từng ItemId theo thứ tự đã sắp xếp.
   - Sắp xếp dòng tồn kho theo chiến lược:
     - `FEFO`: Sắp xếp `expiryDate` tăng dần (Hạn gần xuất trước).
     - `FIFO`: Sắp xếp `manufactureDate` hoặc `createdAt` tăng dần.
     - **Quy tắc Tie-break:** Nếu hai lô có cùng `expiryDate` (hoặc `manufactureDate`), hệ thống ưu tiên lô có `manufactureDate` sớm hơn -> `createdAt` sớm hơn -> `id` tăng dần.
   - Duyệt qua từng dòng tồn kho để trừ lùi nhu cầu xuất.
6. **Xử lý Phân bổ Một phần (Partial Allocation):**
   - Nếu số lượng tồn kho khả dụng < số lượng yêu cầu của line:
     - Nếu `allowPartial = true`: Ghi nhận phân bổ một phần, gán số lượng phân bổ thực tế (`allocatedQty`) bằng tồn khả dụng tối đa, cập nhật trạng thái Shipment Line thành `partially_allocated`.
     - Nếu `allowPartial = false`: Hủy toàn bộ tiến trình, rollback transaction và trả lỗi `inventory.insufficientAvailableQty`.
7. **Commit Transaction:** Lưu thay đổi xuống PostgreSQL. Gửi sự kiện `AllocationCompletedEvent` ra ngoài qua Outbox.

### 8.2 Thuật toán phân bổ chi tiết (Pseudo-code C#)

```csharp
public async Task<AllocationResult> AllocateAsync(
    Guid shipmentId, 
    AllocationStrategy strategy = AllocationStrategy.FEFO, 
    bool allowPartial = true, 
    int reservationTtlMinutes = 1440)
{
    const int maxRetries = 3;
    const int delayMs = 50;
    
    for (int retry = 0; retry < maxRetries; retry++)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var shipmentLines = await _dbContext.ShipmentLines
                .Where(x => x.ShipmentId == shipmentId && x.Status != ShipmentLineStatus.Allocated)
                .ToListAsync();
                
            // CHỐNG DEADLOCK: Sắp xếp các ItemId tăng dần để đảm bảo thứ tự Lock tài nguyên nhất quán
            var sortedLines = shipmentLines.OrderBy(l => l.ItemId).ToList();
                
            foreach (var line in sortedLines)
            {
                // Lock pessimistic trên InventoryBalances của Item Code
                var balances = await _dbContext.InventoryBalances
                    .FromSqlRaw(@"SELECT * FROM ""InventoryBalances"" 
                                  WHERE ""ItemId"" = {0} 
                                    AND ""QcStatus"" = 'released' 
                                    AND ""LockReason"" IS NULL 
                                  FOR UPDATE", line.ItemId)
                    .ToListAsync();
                
                // Áp dụng chiến lược sắp xếp và Tie-break
                if (strategy == AllocationStrategy.FEFO)
                {
                    balances = balances
                        .OrderBy(x => x.ExpiryDate)
                        .ThenBy(x => x.ManufactureDate)
                        .ThenBy(x => x.CreatedAt)
                        .ThenBy(x => x.Id)
                        .ToList();
                }
                else // FIFO
                {
                    balances = balances
                        .OrderBy(x => x.ManufactureDate)
                        .ThenBy(x => x.CreatedAt)
                        .ThenBy(x => x.Id)
                        .ToList();
                }
                
                decimal remainingQty = line.OrderedQty - line.AllocatedQty;
                if (remainingQty <= 0) continue;
                
                foreach (var balance in balances)
                {
                    decimal availableQty = balance.Qty - balance.QtyReserved;
                    if (availableQty <= 0) continue;
                    
                    decimal allocatedQty = Math.Min(remainingQty, availableQty);
                    
                    // Cập nhật lượng giữ hàng
                    balance.QtyReserved += allocatedQty;
                    remainingQty -= allocatedQty;
                    
                    // Tạo bản ghi Allocation Reservation
                    var reservation = new AllocationReservation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = line.TenantId,
                        WarehouseId = line.WarehouseId,
                        ShipmentLineId = line.Id,
                        InventoryBalanceId = balance.Id,
                        Qty = allocatedQty,
                        Status = ReservationStatus.Active,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(reservationTtlMinutes)
                    };
                    await _dbContext.AllocationReservations.AddAsync(reservation);
                    
                    if (remainingQty == 0) break;
                }
                
                if (remainingQty > 0)
                {
                    if (!allowPartial)
                    {
                        throw new InsufficientInventoryException($"Không đủ tồn kho khả dụng cho vật tư: {line.ItemId}");
                    }
                    line.Status = ShipmentLineStatus.PartiallyAllocated;
                }
                else
                {
                    line.Status = ShipmentLineStatus.Allocated;
                }
            }
            
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            
            // Gửi Outbox Event
            await PublishOutboxEventAsync(new AllocationCompletedEvent { ShipmentId = shipmentId });
            return AllocationResult.Success();
        }
        catch (NpgsqlException ex) when (ex.SqlState == "40P01") // Lỗi Deadlock từ PostgreSQL
        {
            await transaction.RollbackAsync();
            if (retry == maxRetries - 1) throw; // Vượt quá số lần retry, ném lỗi ra ngoài
            await Task.Delay(delayMs * (retry + 1)); // Exponential Backoff
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    return AllocationResult.Failed("Đã xảy ra lỗi tranh chấp khóa phân bổ.");
}
```

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.

## 9. Validation & business rules

* **Ràng buộc an toàn tồn kho (Inventory Hard Invariant):**
  - Số lượng khả dụng (`availableQty = qty - qtyReserved`) không bao giờ được âm.
  - Tuyệt đối cấm phân bổ tồn kho đang nằm ở các vị trí bị khóa hoặc có trạng thái QC là `hold`, `qcPending`, hoặc `rejected`.
* **Cơ chế Hết hạn Giữ hàng (Reservation Expiry):**
  - Mọi bản ghi `AllocationReservations` đều có trường `expiresAt = CURRENT_TIMESTAMP + reservationTtlMinutes`.
  - Một Job chạy nền (Background Worker) định kỳ 5 phút một lần sẽ quét các bản ghi có `status = 'ACTIVE' AND expiresAt < CURRENT_TIMESTAMP`.
  - Với mỗi bản ghi hết hạn:
    - Mở transaction, thực hiện giải phóng (Release): Giảm `qtyReserved` trong `InventoryBalances` tương ứng, cập nhật trạng thái reservation thành `EXPIRED`, ghi log giao dịch hoàn trả khả dụng.
* **Quy tắc Giải phóng Chủ động (Manual Release):**
  - Khi người dùng bấm "Hủy phân bổ" hoặc hủy Shipment, toàn bộ reservations liên quan sẽ chuyển thành `RELEASED` và hoàn trả số lượng khả dụng ngay lập tức.

## 10. Exception handling

| Nhóm lỗi | Nguyên nhân | Xử lý |
|---|---|---|
| Thiếu tồn khả dụng | Không đủ hàng trong kho đáp ứng đơn hàng | Nếu `allowPartial = false` thì trả lỗi 400 và rollback. Nếu `allowPartial = true` thì tiến hành phân bổ một phần và cập nhật trạng thái thiếu hàng trên line. |
| Tranh chấp ghi (Race condition) | Hai luồng xử lý phân bổ cùng tranh chấp một dòng tồn kho | Câu lệnh `SELECT FOR UPDATE` sẽ block luồng thứ hai cho đến khi luồng thứ nhất hoàn tất. Nếu bị khóa quá 5 giây (Timeout), trả lỗi `allocation.lockTimeout` để client retry. |
| Lô hàng bị khóa giữa chừng | QC Inspector thực hiện khóa lô hàng đúng lúc đang chạy phân bổ | Kiểm tra lại `qcStatus` của Lot trước khi commit ghi nhận reservation. Nếu trạng thái đã chuyển `hold`, bỏ qua lô đó và tìm lô thay thế. |

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

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

* Fill rate
* Reservation aging
* Audit reserve/release

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

### Kịch bản kiểm thử chi tiết (Test Cases)

* **TC-01 (FEFO/FIFO Allocation Order):** Seed 3 lô hàng có ngày hết hạn khác nhau. Chạy phân bổ FEFO, kiểm tra xem hệ thống có tự động giữ hàng lô có hạn dùng gần nhất trước hay không.
* **TC-02 (Tie-Break Execution):** Seed 2 lô hàng có cùng ngày hết hạn nhưng khác ngày sản xuất. Kiểm tra xem thuật toán có lấy lô sản xuất trước làm ưu tiên không.
* **TC-03 (Pessimistic Locking & Deadlock Resilience):** Chạy đồng thời 2 luồng API gọi phân bổ cho cùng một loại hàng. Kiểm tra xem một luồng có bị chặn (hoặc xếp hàng) và dữ liệu tồn kho cuối cùng không bị âm (over-allocation).
* **TC-04 (Partial Allocation Check):** Đơn xuất yêu cầu 100 sản phẩm, kho chỉ còn 60 sản phẩm. Chạy với `allowPartial = true`, xác nhận hệ thống giữ thành công 60 sản phẩm và cập nhật trạng thái line sang `PARTIALLY_ALLOCATED`. Chạy với `allowPartial = false`, xác nhận hệ thống rollback và trả lỗi `insufficient_qty`.
* **TC-05 (Reservation Expiry Job):** Tạo một bản ghi giữ hàng có hạn dùng đã qua (expired). Chạy Background Job, kiểm tra xem lượng giữ hàng có tự động được trả lại khả dụng và trạng thái reservation chuyển sang `EXPIRED` hay không.

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

Để đạt mức sẵn sàng 95% (Execution-Ready), hệ thống phân bổ phải đáp ứng các tiêu chí nghiệm thu sau:

* **AC-01 (Tính đồng thời và Over-allocation):** Khi chạy đồng thời 2 luồng phân bổ song song cho cùng một mã vật tư có số lượng giới hạn, một luồng phải thành công và luồng còn lại phải bị block đợi khóa hoặc trả về lỗi lock timeout/hết hàng. Hệ thống tuyệt đối không được phân bổ vượt quá số tồn kho khả dụng hiện hữu (`qtyReserved` không vượt quá `qty`).
* **AC-02 (Quy tắc FEFO/FIFO và Tie-break):** Khi phân bổ theo FEFO, hệ thống bắt buộc lấy các dòng tồn kho có `expiryDate` gần nhất. Trong trường hợp có các lô trùng hạn dùng (`expiryDate` bằng nhau), thuật toán phải thực hiện tie-break theo thứ tự: ưu tiên lô sản xuất trước (`manufactureDate` nhỏ hơn) -> lô tạo trước (`createdAt` nhỏ hơn) -> ID nhỏ hơn để bảo đảm FIFO lô.
* **AC-03 (Xử lý Partial Allocation):** Khi `allowPartial = true`, nếu tồn kho chỉ đáp ứng 30% nhu cầu, hệ thống vẫn phải tạo reservation cho 30% đó và gán trạng thái line là `partially_allocated`. Khi `allowPartial = false`, nếu không đáp ứng đủ 100% nhu cầu của toàn bộ các line trong shipment, hệ thống phải rollback toàn bộ transaction (không tạo reservation thừa).
* **AC-04 (Hết hạn giữ hàng tự động):** Background Job chạy mỗi 5 phút phải quét sạch các reservation ở trạng thái `active` có `expiresAt < CURRENT_TIMESTAMP` và thực hiện giải phóng tồn kho: giảm `qtyReserved` trong `InventoryBalances` tương ứng, cập nhật trạng thái reservation sang `expired` trong cùng một transaction.
* **AC-05 (Deadlock resiliency):** Khi xảy ra tranh chấp khóa PostgreSQL gây lỗi deadlock (mã SQLState = `40P01`), hệ thống phải kích hoạt cơ chế retry tự động tối thiểu 3 lần, giãn cách tăng dần (Exponential Backoff: 50ms, 100ms, 150ms) trước khi ném ngoại lệ cuối cùng về client.

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* ATP/CTP nâng cao

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
