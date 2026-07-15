# PHASE 16: Serial tracking

## Execution spec maturity
 
- **Mức hiện tại:** ✅ Hoàn thành Đặc tả (100% Ready to Execute)
- **Đánh giá:** Đã hoàn thiện toàn diện chi tiết 100% đặc tả kỹ thuật: DDL SQL đầy đủ cho PostgreSQL, DTO APIs chi tiết bằng camelCase, thuật toán C# validate quét serial khi lấy hàng, và kịch bản test chi tiết.
- **Khi cần upgrade:** Upgrade nếu serial cần bảo hành, trạng thái trả hàng hoặc tích hợp thiết bị scan riêng.
 
## 1. Mục tiêu
 
Truy vết từng đơn vị sản phẩm bằng Serial Number.
 
Base code và các phân hệ nền tảng đã sẵn sàng. Module Serial tracking sẽ hoạt động độc lập và liên kết với Inventory/Outbound.
 
## 2. Phạm vi
 
Truy vết từng đơn vị sản phẩm bằng Serial Number.
 
### In scope
 
* Tạo module Serial tracking
* Seed permission/rule liên quan
* Cập nhật menu và route
 
### Non-negotiable output
 
* Có database contract hoạt động trên PostgreSQL.
* Có API contract đầu ra chuẩn camelCase.
* Có giao diện di động scan serial khi nhận hàng và xuất hàng.
* Có kịch bản kiểm thử tích hợp tự động verify_serial.ps1.
 
## 3. Điều kiện đầu vào
 
Stage 1 MVP đã ổn định.
 
### Readiness checklist
 
* Phase phụ thuộc (Phase 15) đã hoàn thành và kiểm thử thành công.
* Master data sản phẩm hỗ trợ bật/tắt serial tracking.
* Quyền hệ thống đã được seed đầy đủ.
 
## 4. Setup
 
* Tạo module Serial tracking
* Seed permission/rule liên quan
* Cập nhật menu và route
 
### Cấu trúc module đề xuất
 
```text
backend/modules/Nexustock.Modules.Serial/
frontend/src/app/admin/serial/
frontend/src/app/mobile/serial/
```
 
### Permission seed đề xuất
 
* serial.read
* serial.create
* serial.update
* serial.execute
 
---
 
## 5. Database
 
| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `SerialNumbers` | Serial | Unique item+serial, status, location |
| `SerialEvents` | Timeline serial | Receive,QC,pick,pack,ship,return |
 
#### Cấu trúc bảng SQL chi tiết cho PostgreSQL:
 
```sql
-- 1. Bảng quản lý Serial Numbers
CREATE TABLE serial_numbers (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    item_id UUID NOT NULL,                        -- Liên kết bảng products
    serial_no VARCHAR(100) NOT NULL,
    location_id UUID NOT NULL,                    -- Vị trí kệ lưu trữ hiện tại
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE', -- RECEIVED, ACTIVE, PICKED, SHIPPED, LOCKED
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    row_version INT NOT NULL DEFAULT 1            -- Concurrency Token
);
 
CREATE UNIQUE INDEX uq_serials_tenant_item_no ON serial_numbers(tenant_id, item_id, serial_no);
CREATE INDEX idx_serials_tenant_location ON serial_numbers(tenant_id, location_id);
CREATE INDEX idx_serials_tenant_status ON serial_numbers(tenant_id, status);
 
-- 2. Bảng lưu lịch sử sự kiện hoạt động của Serial
CREATE TABLE serial_events (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    serial_id UUID NOT NULL REFERENCES serial_numbers(id) ON DELETE CASCADE,
    event_type VARCHAR(50) NOT NULL,              -- RECEIVE, QC, PICK, PACK, SHIP, RETURN
    from_location_id UUID,
    to_location_id UUID,
    reference_id UUID,                            -- ID của InboundOrder/OutboundOrder/PickTask
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL
);
 
CREATE INDEX idx_serial_events_tenant_serial ON serial_events(tenant_id, serial_id);
 
-- 3. Nâng cấp bảng products hiện có để hỗ trợ bật/tắt quản lý Serial
ALTER TABLE products ADD COLUMN is_serial_tracked BOOLEAN NOT NULL DEFAULT FALSE;
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
 
---
 
## 6. Backend/API
 
| API | Mục đích | Ghi chú triển khai |
|---|---|---|
| `POST /api/serials/receive` | Nhận serial | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/serials/validate` | Validate scan | Có auth, validation, trace ID và response lỗi chuẩn. |
| `GET /api/serials/{serialNo}` | Tra cứu | Có auth, validation, trace ID và response lỗi chuẩn. |
| `POST /api/serials/{id}/status` | Đổi trạng thái | Có auth, validation, trace ID và response lỗi chuẩn. |
 
#### Chi tiết các endpoint API (JSON camelCase):
 
* **POST /api/serials/receive** (Đăng ký nhận serial mới tại kệ):
  - Request: `{ "itemId": "f8e8f296-f0ab-4fac-adae-7ecdfe5b268e", "serialNo": "SN-REP-E2E-001", "locationId": "00000000-0000-0000-0000-000000000041" }`
  - Response: `{ "id": "e3b4c5d6-1234-5678-abcd-ef0123456789", "serialNo": "SN-REP-E2E-001", "status": "RECEIVED" }`
 
* **POST /api/serials/validate** (Xác thực quét serial khi picking/packing):
  - Request: `{ "itemId": "f8e8f296-f0ab-4fac-adae-7ecdfe5b268e", "serialNo": "SN-REP-E2E-001", "currentLocationId": "00000000-0000-0000-0000-000000000041" }`
  - Response: `{ "valid": true, "message": "Serial hợp lệ để thực hiện lấy hàng." }`
 
* **GET /api/serials/{serialNo}** (Tra cứu lịch sử serial):
  - Response: `{ "id": "e3b4c5d6-1234-5678-abcd-ef0123456789", "serialNo": "SN-REP-E2E-001", "status": "RECEIVED", "events": [ { "eventType": "RECEIVE", "createdAt": "2026-07-15T09:00:00Z", "createdBy": "admin" } ] }`
 
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
| Serial receive scan | Nhập serial | Có loading, empty, error, filter, pagination và quyền theo action. |
| Serial lookup | Truy vết | Có loading, empty, error, filter, pagination và quyền theo action. |
| Serial pick/pack | Bắt buộc scan | Có loading, empty, error, filter, pagination và quyền theo action. |

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
 
1. **Item cấu hình serial**: Sản phẩm trong master data được đánh dấu `is_serial_tracked = true`.
2. **Nhập kho nhận serial**: Khi nhận hàng PO, nhân viên quét từng mã serial để đăng ký trạng thái `RECEIVED`.
3. **QC & Lưu trữ**: Cập nhật trạng thái serial sang `ACTIVE` sau khi thông qua QC và cất hàng vào kệ.
4. **Lấy hàng quét serial (Picking)**: Nhân viên quét mã serial thực tế tại kệ. Hệ thống kiểm tra tính hợp lệ.
5. **Đóng gói & Xuất kho (Pack & Ship)**: Trạng thái chuyển sang `SHIPPED`, kết thúc vòng đời hoạt động chính.
 
#### Chi tiết Thuật toán & Mã nguồn giả lập (C#):
 
##### 8.1 Thuật toán Đăng ký nhận Serial (Receive)
```csharp
public async Task<SerialDto> ReceiveSerialAsync(Guid tenantId, ReceiveSerialDto dto, string operatorName)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    try
    {
        // 1. Kiểm tra sản phẩm có quản lý serial không
        var product = await _masterDataContext.Products.FirstOrDefaultAsync(p => p.Id == dto.ItemId);
        if (product == null || !product.IsSerialTracked)
            throw new Exception("Sản phẩm không áp dụng quản lý mã Serial.");
 
        // 2. Kiểm tra trùng lặp serial hoạt động
        var existing = await _dbContext.SerialNumbers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ItemId == dto.ItemId && s.SerialNo == dto.SerialNo);
        if (existing != null && existing.Status != "SHIPPED")
            throw new Exception($"Mã serial {dto.SerialNo} đã tồn tại trong kho.");
 
        // 3. Khởi tạo bản ghi serial mới
        var serial = new SerialNumber
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = dto.ItemId,
            SerialNo = dto.SerialNo,
            LocationId = dto.LocationId,
            Status = "RECEIVED",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = operatorName
        };
        await _dbContext.SerialNumbers.AddAsync(serial);
 
        // 4. Ghi nhận sự kiện Timeline
        var serialEvent = new SerialEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SerialId = serial.Id,
            EventType = "RECEIVE",
            ToLocationId = dto.LocationId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = operatorName
        };
        await _dbContext.SerialEvents.AddAsync(serialEvent);
 
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return MapToDto(serial);
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```
 
##### 8.2 Thuật toán Xác thực Serial khi Picking (Validate)
```csharp
public async Task<bool> ValidateSerialForPickAsync(Guid tenantId, ValidateSerialDto dto)
{
    var serial = await _dbContext.SerialNumbers
        .FirstOrDefaultAsync(s => s.TenantId == tenantId 
                               && s.ItemId == dto.ItemId 
                               && s.SerialNo == dto.SerialNo);
 
    if (serial == null)
        throw new Exception("Mã serial không tồn tại trong kho.");
 
    if (serial.Status != "ACTIVE")
        throw new Exception($"Trạng thái serial không hợp lệ để xuất. Trạng thái hiện tại: {serial.Status}");
 
    if (serial.LocationId != dto.CurrentLocationId)
        throw new Exception("Serial nằm ở vị trí kệ khác với yêu cầu lấy hàng.");
 
    return true;
}
```
 
### Flow guardrails
 
* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.
 
## 9. Validation & business rules
 
* **Serial duy nhất:** Không cho phép 2 thực thể Serial hoạt động trùng mã trong cùng một Tenant.
* **Quy trình chuyển trạng thái:** Chỉ cho phép chuyển trạng thái theo đúng sơ đồ: `RECEIVED` -> `ACTIVE` -> `PICKED` -> `SHIPPED`.
* **Ràng buộc xuất kho:** Các mặt hàng bật Serial tracking bắt buộc phải quét Serial chuẩn xác mới cho phép hoàn thành tác vụ Picking/Packing.
 
### Validation nền bắt buộc
 
* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.
 
## 10. Exception handling
 
* **Duplicate serial:** Báo lỗi 409 Conflict khi đăng ký serial đã tồn tại và đang hoạt động.
* **Invalid status transition:** Báo lỗi 400 Bad Request khi chuyển trạng thái sai quy trình (ví dụ: `RECEIVED` thẳng lên `SHIPPED`).
* **Location Mismatch:** Báo lỗi 400 khi vị trí quét serial không khớp vị trí lưu trữ thực tế trên hệ thống.
 
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
 
* **Timeline serial:** Hiển thị chi tiết toàn bộ lịch sử vòng đời của một mã serial từ lúc nhận, kiểm định, lấy hàng cho đến xuất xưởng.
* **Audit status:** Ghi nhận rõ ràng ai là người quét, quét tại thiết bị nào và thời gian thực hiện.
 
### Log và trace
 
* Mỗi request có trace ID.
* Command quan trọng ghi audit log.
* Entity nghiệp vụ chính ghi activity timeline.
* Job nền và integration event truyền trace ID khi liên quan flow gốc.
* Log không chứa password, token, secret hoặc dữ liệu nhạy cảm không mask.
 
### KPI đề xuất
 
* Tỷ lệ quét lỗi / quét lại mã serial của nhân viên.
* Thời gian xử lý lấy hàng trung bình trên mỗi serial.
 
## 12. Test plan
 
* **TC-01 (Receive Serial):** Tạo sản phẩm bật serial tracking. Gọi API đăng ký serial `SN-TEST-001` tại kệ `LOC-A-01`. Xác nhận trạng thái `RECEIVED`.
* **TC-02 (Duplicate Block):** Gọi tiếp API đăng ký trùng serial `SN-TEST-001`. Xác nhận hệ thống chặn và trả lỗi 409 Conflict.
* **TC-03 (Validate Serial For Pick):** Gọi API kiểm tra tính hợp lệ của `SN-TEST-001` tại vị trí kệ `LOC-A-01` khi đã ở trạng thái `ACTIVE`. Đảm bảo trả về `valid = true`.
* **TC-04 (Invalid Pick Transition):** Thử gọi kiểm tra serial `SN-TEST-001` tại kệ khác `LOC-A-02`. Đảm bảo hệ thống chặn và trả lỗi sai vị trí.
 
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
 
* **AC-01 (Nhận & Đăng ký Serial):** Hệ thống cho phép quét nhận và đăng ký mã serial duy nhất cho sản phẩm, kiểm soát trùng lặp chặt chẽ.
* **AC-02 (Ràng buộc Picking):** Nhân viên kho bắt buộc phải quét serial và hệ thống tự động xác thực đúng vị trí kệ/trạng thái mới cho phép hoàn tất lấy hàng.
* **AC-03 (Truy vết Timeline):** Hiển thị rõ lịch sử dòng thời gian di chuyển và thay đổi trạng thái của từng mã serial cụ thể trên Web Admin.
 
### Definition of done
 
* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.
 
## 14. Out of scope
 
* Tích hợp bảo hành (Warranty) và cổng thông tin bảo hành cho end-user.
 
## 15. Dependencies
 
* Stage 1 + Phase 15 (LPN Pallet Management) hoạt động ổn định.
 
## 16. Maintenance notes
 
* Đảm bảo pessimistic locking hoặc optimistic concurrency trên bảng `serial_numbers` tránh tình trạng quét trùng một mã serial tại 2 thiết bị cầm tay đồng thời.
* Luôn cập nhật đồng bộ trạng thái tồn kho thực tế (`qty_on_hand` và `lpn_id` nếu có) đi kèm với serial numbers.
 
## 17. Extension points
 
* Hỗ trợ gán dải serial tự động (Serial range) khi nhận hàng lô lớn từ nhà cung cấp.
 
## 18. Rollback notes
 
* Khi rollback, tắt permission và ẩn menu serial trên UI. Không xoá các bản ghi giao dịch serial lịch sử đã phát sinh trong DB.







