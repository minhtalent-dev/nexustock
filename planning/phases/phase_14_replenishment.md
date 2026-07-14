# PHASE 14: Replenishment

## Execution spec maturity

- **Mức hiện tại:** ✅ Hoàn thành (100% Completed)
- **Đánh giá:** Đã hoàn thiện toàn diện chi tiết 95% đặc tả kỹ thuật: cấu trúc bảng `ReplenishmentRules` và `ReplenishmentTasks` kèm khóa ngoại, index PostgreSQL, API DTOs camelCase chi tiết, thuật toán quét min/max và chọn nguồn bổ sung (FIFO/FEFO) tối ưu, tích hợp sâu vào hệ thống nhiệm vụ di động `MobileTasks` của nhân viên kho, kịch bản test integration chi tiết và thiết kế màn hình Next.js.
- **Khi cần upgrade:** Upgrade nếu cần thêm tính năng dự báo nhu cầu (Forecast Replenishment) dựa trên AI hoặc tích hợp định tuyến xe nâng nâng cao.

---

## 1. Mục tiêu

Tự động giám sát tồn kho tại các vị trí lấy hàng (Pick Face), phát hiện khi tồn khả dụng xuống dưới mức tối thiểu (Min) và sinh nhiệm vụ bổ sung hàng (Replenishment Task) từ các vị trí lưu trữ lưu không (Bulk/Reserve Locations) về Pick Face để bảo đảm không gián đoạn luồng xuất hàng.

---

## 2. Phạm vi

### In scope

* Triển khai cấu trúc dữ liệu cho Rule cấu hình Min/Max và Task bổ sung.
* Phát triển API tự động quét, phát hiện thiếu hụt và sinh các nhiệm vụ bổ sung hàng.
* Triển khai thuật toán chọn nguồn hàng tối ưu từ kho lưu trữ (Bulk) theo FEFO/FIFO, đảm bảo chỉ lấy hàng đã duyệt QC và vị trí không bị khóa.
* Tích hợp nhiệm vụ bổ sung vào hàng đợi công việc di động (`MobileTasks` - Pool model) để nhân viên dùng thiết bị RF/Mobile quét thực hiện.
* Phát triển giao diện quản lý trên Next.js và màn hình giả lập handheld.

---

## 3. Điều kiện đầu vào

### Readiness checklist

* **Phase 06 (Inventory & movement)** đã hoàn thành và hoạt động ổn định (đã có cơ chế kiểm soát vị trí và dịch chuyển).
* **Phase 09 (RF/mobile core scan)** đã hoàn thành (đã có cơ chế Pool model gán nhiệm vụ và quét mã).
* **Phase 11 (Rule engine foundation)** đã hoàn thành.

---

## 4. Setup

### Cấu trúc module đề xuất

```text
backend/modules/replenishment/
frontend/features/replenishment/
planning/phases/phase_14_replenishment.md
```

### Permission seed đề xuất

* `replenishment.read`: Xem cấu hình rules và danh sách tasks.
* `replenishment.create`: Tạo cấu hình rule hoặc kích hoạt tiến trình sinh task thủ công.
* `replenishment.update`: Chỉnh sửa rule hoặc cập nhật trạng thái task.
* `replenishment.execute`: Thực hiện nhiệm vụ bổ sung trên thiết bị di động.

---

## 5. Database

Để tránh trùng lặp dữ liệu và phức tạp hóa hệ thống, số dư tồn kho tại Pick Face (`PickFaceBalances`) sẽ được truy vấn trực tiếp từ bảng `inventories` hiện tại bằng cách lọc theo thuộc tính của Vị trí (`location.type = 'PickFace'`).

### 1. Bảng Cấu hình ngưỡng bổ sung (`replenishment_rules`)

Cấu hình mức tồn kho tối thiểu (Min) và tối đa (Max) cho từng vật tư tại từng vị trí Pick Face cụ thể.

```sql
CREATE TABLE replenishment_rules (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    item_id UUID NOT NULL,
    location_id UUID NOT NULL, -- Vị trí Pick Face (chỉ cho phép LocationType = 'PickFace')
    min_qty DECIMAL(18,6) NOT NULL CHECK (min_qty >= 0),
    max_qty DECIMAL(18,6) NOT NULL CHECK (max_qty > min_qty),
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    xmin XID NOT NULL -- optimistic concurrency token
);

CREATE UNIQUE INDEX uq_replenishment_rules_tenant_item_loc ON replenishment_rules(tenant_id, item_id, location_id);
CREATE INDEX idx_replenishment_rules_tenant_item ON replenishment_rules(tenant_id, item_id);
```

### 2. Bảng Nhiệm vụ bổ sung hàng (`replenishment_tasks`)

Theo dõi các yêu cầu và lịch sử thực hiện dịch chuyển hàng từ kho lưu trữ về Pick Face.

```sql
CREATE TABLE replenishment_tasks (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    rule_id UUID REFERENCES replenishment_rules(id) ON DELETE SET NULL,
    item_id UUID NOT NULL,
    source_location_id UUID NOT NULL, -- Vị trí lấy hàng (Bulk / Reserve)
    target_location_id UUID NOT NULL, -- Vị trí trả hàng (Pick Face)
    lot_no VARCHAR(100) NOT NULL,     -- Số lô hàng cần bổ sung
    requested_qty DECIMAL(18,6) NOT NULL CHECK (requested_qty > 0),
    actual_qty DECIMAL(18,6) NOT NULL DEFAULT 0.0 CHECK (actual_qty >= 0.0),
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING', -- PENDING, ASSIGNED, COMPLETED, CANCELLED
    mobile_task_id UUID,              -- Liên kết sang bảng mobile_tasks để nhân viên nhận việc
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP,
    updated_by VARCHAR(100),
    xmin XID NOT NULL
);

CREATE INDEX idx_replenishment_tasks_tenant_status ON replenishment_tasks(tenant_id, status);
CREATE INDEX idx_replenishment_tasks_tenant_item ON replenishment_tasks(tenant_id, item_id);
```

---

## 6. Backend/API

### API Endpoints

#### 1. `POST /api/replenishment/generate`
* **Mục đích**: Chạy tiến trình quét tồn kho tại các Pick Face, tự động tính toán thiếu hụt so với `min_qty` và sinh các nhiệm vụ bổ sung hàng.
* **Request Body (camelCase)**:
```json
{
  "strategy": "FEFO" // FEFO, FIFO
}
```
* **Response (camelCase)**:
```json
{
  "success": true,
  "tasksGenerated": 3,
  "message": "Đã quét và tự động sinh 3 nhiệm vụ bổ sung hàng."
}
```

#### 2. `GET /api/replenishment/tasks`
* **Mục đích**: Lấy danh sách nhiệm vụ bổ sung kèm theo bộ lọc trạng thái.
* **Response (camelCase)**:
```json
{
  "items": [
    {
      "id": "a2b3c4d5-1234-5678-abcd-ef0123456789",
      "itemCode": "ITEM-CABLE-01",
      "itemName": "Cáp đồng Cat6",
      "sourceLocationCode": "LOC-BULK-01",
      "targetLocationCode": "LOC-PICK-12",
      "lotNo": "LOT-20260714-01",
      "requestedQty": 150.0,
      "actualQty": 0.0,
      "status": "PENDING",
      "createdAt": "2026-07-14T08:54:12Z"
    }
  ],
  "totalCount": 1
}
```

#### 3. `POST /api/replenishment/tasks/{id}/complete`
* **Mục đích**: Ghi nhận hoàn tất bổ sung hàng (gọi từ thiết bị handheld khi nhân viên kho quét xác nhận).
* **Request Body (camelCase)**:
```json
{
  "actualQty": 150.0,
  "scannedSourceLocation": "LOC-BULK-01",
  "scannedLotNo": "LOT-20260714-01",
  "scannedTargetLocation": "LOC-PICK-12"
}
```
* **Response (camelCase)**:
```json
{
  "success": true,
  "message": "Nhiệm vụ bổ sung hoàn thành. Số dư tồn kho đã được cập nhật."
}
```

---

## 7. Frontend/RF/mobile

### Giao diện Quản lý Phân hệ Bổ sung (Next.js Dashboard SPA)

1. **Màn hình Cấu hình Min/Max (Replenishment Rules)**:
   - Danh sách quy tắc bổ sung theo vị trí Pick Face.
   - Nút "Thêm quy tắc": Cho chọn vật tư, chọn vị trí Pick Face, nhập số lượng Min, số lượng Max. Có validation đảm bảo `Max > Min`.
2. **Màn hình Giám sát Hàng đợi Bổ sung (Replenishment Queue)**:
   - Hiển thị danh sách các task đang ở trạng thái `Pending`, `Assigned`, `Completed`.
   - Nút "Run Replenishment Engine" (Primary): Kích hoạt API tự động quét và sinh task.

### Màn hình Handheld RF/Mobile (RF Handheld Touchpoint)

Tích hợp vào module quét mã di động:
- **Replenishment Task Screen**: Nhân viên kho xem nhiệm vụ bổ sung được gán (hoặc tự nhận từ Pool).
- **Yêu cầu quét**:
  1. Quét vị trí nguồn (Source Location) để xác nhận lấy hàng -> Báo lỗi nếu quét sai vị trí.
  2. Quét mã Lot của hàng hóa để xác nhận lấy đúng lô.
  3. Quét vị trí đích (Target Location) để xác nhận đặt hàng vào Pick Face.
  4. Nhập số lượng thực tế dịch chuyển.

---

## 8. Execution flow

### 8.1 Thuật toán quét và tự động sinh Task bổ sung
1. **Tìm kiếm pick face thiếu hụt**: Quét toàn bộ `replenishment_rules` của tenant. Với mỗi rule, tính lượng tồn kho khả dụng hiện tại tại Pick Face:
   `availableQty = Sum(inventories.qty_on_hand - inventories.qty_reserved) tại location_id của rule`.
2. **Xác định lượng cần bổ sung**: Nếu `availableQty < rule.min_qty`, tính lượng thiếu hụt:
   `neededQty = rule.max_qty - availableQty`.
3. **Tìm nguồn hàng phù hợp (Bulk/Reserve)**:
   - Tìm các dòng tồn kho tại các vị trí lưu trữ Bulk/Reserve (`location.type = 'Bulk'`).
   - Lọc chỉ lấy các lô có `QcStatus = 'Release'` và vị trí không bị khóa (`LocationLocks` không chứa `location_id`).
   - Sắp xếp theo chiến lược (FEFO: ưu tiên hạn dùng gần nhất; FIFO: ưu tiên ngày sản xuất).
4. **Phân bổ và giữ hàng tại nguồn**:
   - Duyệt qua danh sách tồn kho Bulk tìm được, trừ lùi số lượng `neededQty`.
   - Thực hiện khóa giữ hàng tại nguồn bằng cách tăng `qty_reserved` của dòng tồn kho Bulk tương ứng (để ngăn các luồng xuất hàng khác lấy mất phần hàng đang bổ sung này).
   - Tạo bản ghi `replenishment_tasks` ghi nhận thông tin lô, nguồn, đích, số lượng yêu cầu.
   - Tạo đồng thời một bản ghi `mobile_tasks` loại `Replenishment` để đẩy vào work pool di động.

### 8.2 Pseudo-code thuật toán bổ sung (C#)

```csharp
public async Task<int> GenerateReplenishmentTasksAsync(Guid tenantId, string strategy = "FEFO")
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    try
    {
        int tasksGenerated = 0;
        
        // 1. Lấy danh sách rules cấu hình
        var rules = await _dbContext.ReplenishmentRules
            .Where(r => r.TenantId == tenantId)
            .ToListAsync();
            
        foreach (var rule in rules)
        {
            // 2. Tính tồn khả dụng hiện tại ở Pick Face
            decimal currentAvailable = await _dbContext.Inventories
                .Where(i => i.TenantId == tenantId && i.LocationId == rule.LocationId && i.ItemId == rule.ItemId)
                .SumAsync(i => i.QtyOnHand - i.QtyReserved);
                
            if (currentAvailable >= rule.min_qty) continue; // Chưa cần bổ sung
            
            decimal neededQty = rule.max_qty - currentAvailable;
            
            // Tìm các task bổ sung đang PENDING hoặc ASSIGNED của pick face này để tránh sinh trùng
            decimal alreadyInFlight = await _dbContext.ReplenishmentTasks
                .Where(t => t.TenantId == tenantId && t.TargetLocationId == rule.LocationId && t.ItemId == rule.ItemId && (t.Status == "PENDING" || t.Status == "ASSIGNED"))
                .SumAsync(t => t.RequestedQty);
                
            neededQty -= alreadyInFlight;
            if (neededQty <= 0) continue; // Đang có task bổ sung trên đường đi
            
            // 3. Tìm nguồn hàng bổ sung ở các vị trí Bulk/Reserve
            var lockedLocationIds = await _dbContext.LocationLocks
                .Where(l => l.TenantId == tenantId)
                .Select(l => l.LocationId)
                .ToListAsync();
                
            var bulkLocations = await _dbContext.Locations
                .Where(l => l.TenantId == tenantId && l.Type == LocationType.Bulk && !lockedLocationIds.Contains(l.Id))
                .Select(l => l.Id)
                .ToListAsync();
                
            var candidates = await _dbContext.Inventories
                .Where(i => i.TenantId == tenantId && i.ItemId == rule.ItemId && bulkLocations.Contains(i.LocationId) && (i.QtyOnHand - i.QtyReserved) > 0)
                .ToListAsync();
                
            // Lọc theo QC Status đã Duyệt (Release) từ Module Inbound
            var lotNos = candidates.Select(c => c.LotNo).Distinct().ToList();
            var releasedLots = await _inboundContext.Lots
                .Where(l => l.TenantId == tenantId && l.ItemId == rule.ItemId && lotNos.Contains(l.LotNo) && l.QcStatus == LotQcStatus.Release)
                .ToDictionaryAsync(l => l.LotNo, l => l);
                
            var activeCandidates = candidates
                .Where(c => releasedLots.ContainsKey(c.LotNo))
                .ToList();
                
            // Sắp xếp theo FIFO/FEFO
            IOrderedEnumerable<Entities.Inventory> sortedCandidates;
            if (strategy == "FIFO")
            {
                sortedCandidates = activeCandidates
                    .OrderBy(c => releasedLots[c.LotNo].ProductionDate ?? DateTime.MaxValue)
                    .ThenBy(c => c.CreatedAt);
            }
            else // FEFO
            {
                sortedCandidates = activeCandidates
                    .OrderBy(c => releasedLots[c.LotNo].ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(c => c.CreatedAt);
            }
            
            // 4. Trừ lùi và tạo Task
            foreach (var sourceInv in sortedCandidates)
            {
                decimal availableAtSource = sourceInv.QtyOnHand - sourceInv.QtyReserved;
                if (availableAtSource <= 0) continue;
                
                decimal qtyToTake = Math.Min(neededQty, availableAtSource);
                
                // Khóa giữ hàng tại Bulk nguồn
                sourceInv.QtyReserved += qtyToTake;
                neededQty -= qtyToTake;
                
                // Tạo Replenishment Task
                var replTask = new ReplenishmentTask
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RuleId = rule.Id,
                    ItemId = rule.ItemId,
                    SourceLocationId = sourceInv.LocationId,
                    TargetLocationId = rule.LocationId,
                    LotNo = sourceInv.LotNo,
                    RequestedQty = qtyToTake,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "SystemEngine"
                };
                
                // Tạo Mobile Task đẩy vào Pool công việc
                var mobileTask = new MobileTask
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Type = "Replenishment",
                    SourceLocationId = sourceInv.LocationId,
                    TargetLocationId = rule.LocationId,
                    ItemId = rule.ItemId,
                    LotNo = sourceInv.LotNo,
                    Qty = qtyToTake,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow
                };
                
                replTask.MobileTaskId = mobileTask.Id;
                
                await _dbContext.ReplenishmentTasks.AddAsync(replTask);
                await _dbContext.MobileTasks.AddAsync(mobileTask);
                
                tasksGenerated++;
                if (neededQty <= 0) break;
            }
        }
        
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return tasksGenerated;
    }
    catch (Exception)
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### 8.3 Chi tiết luồng cập nhật tồn kho & Giải phóng tồn giữ hàng (Inventory & Reservation Update Flow)
Khi nhân viên xác nhận hoàn tất bổ sung hàng trên thiết bị Handheld (gọi API `POST /api/replenishment/tasks/{id}/complete`), hệ thống thực thi trong một Database Transaction:
1. **Tìm và Lock dữ liệu liên quan**:
   - Khóa bản ghi `ReplenishmentTask` theo ID.
   - Khóa dòng tồn kho tại vị trí Bulk nguồn (`Inventories` khớp `source_location_id`, `item_id`, `lot_no`).
   - Khóa/Tìm dòng tồn kho tại vị trí Pick Face đích (`Inventories` khớp `target_location_id`, `item_id`, `lot_no`). Nếu chưa có bản ghi tồn kho cho lô này tại Pick Face, tạo mới.
2. **Cập nhật tồn kho Bulk nguồn**:
   - Trừ số lượng thực tế dịch chuyển (`actualQty`) khỏi `QtyOnHand` của Bulk nguồn.
   - **Đặc biệt (Tránh điểm mù)**: Giải phóng toàn bộ lượng dự trữ ban đầu bằng cách trừ đi số lượng yêu cầu ban đầu (`requestedQty`) khỏi `QtyReserved` của Bulk nguồn. 
   - *Công thức*: `sourceInv.QtyReserved = Math.Max(0, sourceInv.QtyReserved - replTask.RequestedQty)`.
3. **Cập nhật tồn kho Pick Face đích**:
   - Cộng `actualQty` vào `QtyOnHand` tại Pick Face đích.
4. **Xử lý chênh lệch thiếu (Under-replenishment)**:
   - Nếu `actualQty < requestedQty` (ví dụ: Bulk nguồn bị thiếu hàng so với hệ thống ghi nhận):
     - Ghi nhận `ReplenishmentTask.status = 'COMPLETED'` và lưu `ActualQty = actualQty`.
     - Gọi **Exception Framework (Phase 10)** để tự động sinh một phiếu lỗi `InventoryDiscrepancy` ghi nhận thiếu hụt `requestedQty - actualQty` tại vị trí Bulk nguồn phục vụ kiểm kê.
5. **Ghi nhật ký dịch chuyển (Audit Trail)**:
   - Ghi nhận một dòng `InventoryMovement` loại `REPLENISHMENT` để theo dõi vết dịch chuyển vật lý của lô hàng.

### 8.4 Đồng bộ trạng thái với MobileTasks
Để hệ thống Pool model hoạt động trơn tru:
- Khi nhân viên kho nhận nhiệm vụ trên Handheld:
  - Cập nhật `ReplenishmentTask.status = 'ASSIGNED'`.
  - Cập nhật `MobileTask.status = 'ASSIGNED'` và gán `AssignedUser` là username của nhân viên.
- Khi hoàn thành:
  - Cả `ReplenishmentTask` và `MobileTask` chuyển sang trạng thái `COMPLETED`.
- Khi hủy task bổ sung:
  - Cả 2 chuyển sang `CANCELLED`.
  - Giải phóng `QtyReserved` tại Bulk nguồn tương ứng với `requestedQty` của task bị hủy.

---

## 9. Validation & business rules

* **Bảo toàn số lượng tồn kho**:
  - Không được bổ sung số lượng vượt quá số lượng tối đa (`max_qty`) của cấu hình Pick Face.
  - Khi sinh task bổ sung, hệ thống phải cộng dồn cả lượng hàng "đang trên đường đi" (các task bổ sung có trạng thái `Pending` hoặc `Assigned` hướng tới vị trí đó) để tránh sinh trùng lặp công việc.
* **Chọn nguồn an toàn**:
  - Tuyệt đối cấm lấy hàng từ các vị trí Bulk đang bị khóa (`LocationLocks`).
  - Tuyệt đối cấm lấy hàng thuộc các lô chưa qua kiểm định QC hoặc đang bị khóa QC Hold.

---

## 10. Exception handling

| Nhóm lỗi | Nguyên nhân | Xử lý |
|---|---|---|
| Không tìm thấy nguồn bổ sung | Kho Bulk đã hết sạch hàng đạt chất lượng QC hoặc các kệ Bulk bị khóa | Ghi log cảnh báo `replenishment.no_source_available`. Không tạo task lỗi. Hiển thị cảnh báo "Nguy cơ đứt hàng" trên Dashboard để thủ kho nhập hàng mới. |
| Quét sai vị trí trên RF | Nhân viên quét nhầm kệ khi lấy hàng hoặc trả hàng | Trả lỗi `validation.invalid_scanned_location`. Yêu cầu nhân viên quét lại. |
| Sai số lượng thực tế | Số lượng trên kệ Bulk bị thiếu so với hệ thống ghi nhận | Báo lỗi `replenishment.source_qty_mismatch`. Chuyển tiếp luồng xử lý sang **Phase 10 Exception Framework** (tự động tạo phiếu ghi nhận sai lệch tồn kho). |

---

## 11. Observability

* **Stockout Risk Alert**: Hệ thống tự động đẩy cảnh báo lên dashboard nếu Pick Face có tồn kho dưới Min mà không thể sinh task bổ sung do thiếu nguồn Bulk.
* **Task Duration Tracking**: Theo dõi thời gian hoàn thành task bổ sung từ lúc sinh ra đến lúc hoàn thành thực tế để đo lường hiệu suất nhân sự.

---

## 12. Test plan

### Kịch bản kiểm thử chi tiết (Test Cases)

* **TC-01 (Auto Task Generation)**: Cấu hình rule Min = 50, Max = 200 cho Item-A tại kệ PICK-01. Điều chỉnh tồn thực tế tại PICK-01 xuống 30. Chạy tiến trình sinh task, kiểm tra hệ thống có tự sinh task bổ sung số lượng 170 hay không.
* **TC-02 (In-Flight Task Guard)**: Tiếp tục chạy lại tiến trình sinh task khi task của TC-01 vẫn đang ở trạng thái `PENDING`. Kiểm tra hệ thống KHÔNG được sinh thêm task mới (tránh trùng lặp).
* **TC-03 (FEFO Source Priority)**: Có 2 lô hàng tại Bulk: Lô 1 hạn dùng 15/08/2026, Lô 2 hạn dùng 10/08/2026. Chạy bổ sung FEFO, kiểm tra xem hệ thống có chỉ định lấy từ Lô 2 trước hay không.
* **TC-04 (QC Lock Guard)**: Trùng kịch bản TC-03 nhưng Lô 2 đang có trạng thái QC là `Hold`. Xác nhận hệ thống tự động bỏ qua Lô 2 và lấy hàng từ Lô 1.
* **TC-05 (Mobile execution end-to-end)**: Nhân viên nhận task qua handheld di động, quét kiểm tra vị trí nguồn, mã lô, vị trí đích và nhập số lượng hoàn thành. Kiểm tra số dư tồn kho tại kệ Bulk bị trừ, kệ Pick Face được cộng tương ứng.
* **TC-06 (Under-replenishment Handling)**: Gán task bổ sung 100 sản phẩm. Khi quét di động, nhân viên chỉ xác nhận thực tế dịch chuyển 80 sản phẩm. Kiểm tra tồn Bulk nguồn giảm đúng 80, tồn giữ `QtyReserved` Bulk nguồn giảm cả 100 (về 0), tồn Pick Face tăng 80, và hệ thống sinh ra một Exception ticket thiếu 20 sản phẩm.

---

## 13. Acceptance criteria

* **AC-01 (Tự động sinh nhiệm vụ chính xác)**: Hệ thống phát hiện toàn bộ các Pick Face bị hụt hàng dưới Min và tính toán chính xác lượng cần bổ sung.
* **AC-02 (Không trùng lặp)**: Tuyệt đối không sinh trùng lặp các yêu cầu bổ sung cho cùng một kệ khi công việc cũ chưa hoàn thành.
* **AC-03 (Tích hợp Handheld)**: Nhiệm vụ bổ sung hiển thị đầy đủ trên màn hình handheld và yêu cầu quét đúng 3 bước (Source location -> Lot -> Target location) để cập nhật tồn kho an toàn.
* **Definition of Done**:
  - Database migration chạy sạch trên database trống.
  - API chính có test integration pass.
  - UI/RF/mobile flow chính thao tác được end-to-end.
  - Audit/trace hoạt động cho command quan trọng.
  - Exception path chính được test.
  - README hoặc phase note đủ để executor tiếp theo hiểu dependency.
  - Không còn placeholder generic trong phần triển khai phase.

---

## 14. Out of scope

* Forecast replenishment

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

---

## 15. Dependencies

* Stage 1 + phase trước trong Stage 2

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.

---

## 16. Maintenance notes

* Không làm phức tạp MVP
* Feature advanced phải có flag/permission riêng
* Mọi transaction inventory phải atomic

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

---

## 17. Extension points

* Tối ưu thuật toán
* Thêm dashboard nâng cao
* Thêm rule cấu hình sâu hơn

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

---

## 18. Rollback notes

* Tắt permission/menu
* Release reservation/task mở nếu rollback
* Không xóa transaction đã phát sinh

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.
