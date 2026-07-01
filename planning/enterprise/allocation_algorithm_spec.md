# Allocation Algorithm Deep Spec — Nexustock WMS

> **Phase coverage:** Phase 13 (Allocation & reservation)
> **Status:** Execution-ready spec — phải được FOUNDER approve trước khi code Phase 13
> **Nâng maturity:** 90% → 95%

---

## 1. Overview

Module Allocation thực hiện giữ hàng (reserve) từ tồn kho khả dụng cho một đơn xuất kho (Shipment). Đây là nghiệp vụ tranh chấp tài nguyên (concurrent resource contention) — nhiều shipment có thể chạy phân bổ đồng thời, dẫn đến deadlock hoặc race condition nếu không có lock order chuẩn.

**Scope:** Chỉ ghi `AllocationReservations` và cập nhật `qtyReserved` trong `InventoryBalances`. Không trừ tồn kho vật lý — tồn kho chỉ trừ khi Ship (SHIP transaction).

---

## 2. Pre-conditions (DoR cho Phase 13)

- [ ] Phase 11 (Rule Engine) đã pass — rule engine có thể inject strategy override
- [ ] Phase 12 (Putaway slotting) đã pass — `locationId` valid trong InventoryBalances
- [ ] Bảng `AllocationReservations` đã có migration
- [ ] `InventoryBalances` đã có column `qtyReserved` (decimal, NOT NULL, DEFAULT 0, CHECK >= 0)
- [ ] `InventoryBalances` đã có `rowVersion` (byte[]) cho optimistic concurrency

---

## 3. Input Parameters

```json
{
  "shipmentId": "shp_001",
  "warehouseId": "wh_vsip_01",
  "strategy": "FEFO",
  "allowPartial": true,
  "reservationTtlMinutes": 1440,
  "priorityScore": 100,
  "lotFilterRules": []
}
```

**Strategy mapping:**

| Strategy | Sort key | Direction | Use case |
|---|---|---|---|
| FEFO | `lot.expiryDate` | ASC (gần hết hạn trước) | Hàng có hạn dùng (sữa, dược phẩm) |
| FIFO | `inventoryBalance.createdAt` | ASC (nhập trước xuất trước) | Hàng không có expiry date |
| LIFO | `inventoryBalance.createdAt` | DESC (nhập sau xuất trước) | Vật tư cuộn, cáp, theo yêu cầu |
| AUTO | Strategy do Rule Engine inject | — | Override theo rule priority |

**Tie-break rule (bắt buộc):**
- Cùng strategy sort key → tie-break theo `inventoryBalance.id ASC` (deterministic, không random)
- Items không có `expiryDate` (null) khi strategy=FEFO → tự động fallback sang FIFO sort (createdAt ASC)
- Items có `expiryDate` null trong FEFO sort → đẩy xuống cuối danh sách (NULL LAST)

---

## 4. Algorithm Pseudo-code

```
FUNCTION AllocateShipment(input):

  // STEP 1: Load shipment lines cần phân bổ
  shipmentLines = DB.Query(
    SELECT * FROM ShipmentLines
    WHERE shipmentId = @shipmentId
      AND status IN ('pending', 'partially_allocated')
      AND tenantId = @tenantId
  )
  IF shipmentLines.IsEmpty THEN RETURN Error("shipment.noLinesFound")

  // STEP 2: Load candidates tồn kho khả dụng (READ ONLY - chưa lock)
  candidates = DB.Query(
    SELECT ib.*, l.expiryDate
    FROM InventoryBalances ib
    JOIN Lots l ON ib.lotId = l.id
    WHERE ib.tenantId = @tenantId
      AND ib.warehouseId = @warehouseId
      AND ib.inventoryStatus = 'available'
      AND ib.locationLocked = false
      AND l.qcStatus = 'released'
      AND (ib.qty - ib.qtyReserved) > 0
    ORDER BY [strategy_sort_key] [ASC|DESC] NULLS LAST,
             ib.id ASC   -- tie-break: LUÔN ASC để deterministic
  )

  // STEP 3: Tính allocation plan trong memory
  allocationPlan = []
  FOR EACH line IN shipmentLines:
    remaining = line.requiredQty
    FOR EACH candidate IN candidates[item = line.itemId]:
      available = candidate.qty - candidate.qtyReserved
      IF available <= 0 THEN CONTINUE
      take = MIN(available, remaining)
      allocationPlan.ADD({ inventoryBalanceId: candidate.id, lineId: line.id, qty: take })
      remaining -= take
      IF remaining <= 0 THEN BREAK

    IF remaining > 0 AND NOT input.allowPartial:
      RETURN Error("inventory.insufficientAvailableQty", { lineId: line.id, shortage: remaining })

  // STEP 4: LOCK theo id ASC rồi WRITE trong transaction
  // CRITICAL: Luôn lock theo inventoryBalanceId ASC để tránh deadlock
  lockedIds = SORT(allocationPlan.SELECT(x => x.inventoryBalanceId).DISTINCT(), ASC)

  BEGIN TRANSACTION (IsolationLevel = ReadCommitted)
  TRY:
    lockedRows = DB.Query(
      SELECT * FROM InventoryBalances
      WHERE id IN @lockedIds
      ORDER BY id ASC       -- EXPLICIT ASC ORDER - không bao giờ thay đổi
      FOR UPDATE NOWAIT     -- fail fast thay vì block vô hạn
    )

    FOR EACH plan IN allocationPlan:
      row = lockedRows[plan.inventoryBalanceId]
      actualAvailable = row.qty - row.qtyReserved

      IF actualAvailable < plan.qty:
        IF NOT input.allowPartial:
          ROLLBACK
          RETURN Error("inventory.concurrencyShortage")
        plan.qty = actualAvailable  -- điều chỉnh xuống

      DB.INSERT INTO AllocationReservations VALUES (
        NewUlid(), tenantId, plan.lineId, plan.inventoryBalanceId,
        plan.qty, 'active',
        NOW() + INTERVAL @reservationTtlMinutes MINUTES,
        currentUserId
      )

      DB.UPDATE InventoryBalances
        SET qtyReserved = qtyReserved + plan.qty, updatedAt = NOW()
        WHERE id = plan.inventoryBalanceId
          AND (qty - qtyReserved) >= plan.qty  -- double-check constraint

      IF rows_affected = 0:
        ROLLBACK
        RETURN Error("inventory.concurrencyConflict")

    COMMIT
    PUBLISH_OUTBOX_EVENT("AllocationCompleted", { shipmentId, reservations })
    RETURN Success(allocationResult)

  CATCH LockNotAvailableException:
    ROLLBACK
    RETURN Error("allocation.lockTimeout", { retryAfterMs: 2000 })
  CATCH Exception ex:
    ROLLBACK
    LOG_ERROR(ex, traceId)
    RETURN Error("allocation.internalError")
```

---

## 5. Deadlock Prevention — Quy tắc bắt buộc

> **Rule tuyệt đối:** Khi lock nhiều dòng `InventoryBalances` trong cùng 1 transaction, **LUÔN LUÔN** lock theo thứ tự `id ASC`. Không được lock theo thứ tự xuất hiện trong allocation request.

**Vì sao?**

```
Transaction T1: cần lock Balance-A, Balance-B
Transaction T2: cần lock Balance-B, Balance-A

Không có consistent order -> T1 lock A, T2 lock B -> T1 chờ B, T2 chờ A -> DEADLOCK

Với lock order ASC:
T1: lock A rồi B
T2: lock A trước (block, chờ T1 xong) -> NO DEADLOCK
```

**C# implementation:**

```csharp
// CORRECT
var lockedIds = allocationPlan
    .Select(x => x.InventoryBalanceId)
    .Distinct()
    .OrderBy(x => x)   // LUÔN ASC
    .ToList();

// WRONG — dễ deadlock
var lockedIds = allocationPlan
    .Select(x => x.InventoryBalanceId)
    .ToList(); // không sort
```

---

## 6. Retry Strategy (Client-side)

Khi nhận `allocation.lockTimeout`:

| Attempt | Wait before retry |
|---|---|
| 1st retry | 2 giây |
| 2nd retry | 4 giây |
| 3rd retry | 8 giây |
| Fail | Tạo OperationalException type ALLOCATION_LOCK_TIMEOUT |

---

## 7. Partial Allocation Decision Tree

```
shortage = 0?
  YES -> AllocationStatus = "allocated" (100%)
  NO:
    allowPartial = false -> ROLLBACK, Error "insufficientQty"
    allowPartial = true:
      allocate available qty
      ShipmentLine.status = "partially_allocated"
      AllocationStatus = "partially_allocated"
      Create OperationalException SHORTAGE
        -> supervisor quyết định: chờ nhập thêm | hủy | tách đơn
```

---

## 8. Multi-warehouse Notes

- Mỗi allocation request phải có `warehouseId` cụ thể — không cross-warehouse allocation
- Nếu cần chuyển hàng từ kho khác sang, phải qua Cross-docking (Phase 27) hoặc Transfer Order riêng
- Lock order vẫn là `inventoryBalanceId ASC` — không phân biệt warehouseId khi lock

---

## 9. Performance Boundary

| Scenario | Expected | Hard limit |
|---|---|---|
| 1 shipment, 10 lines, 50 balance rows | < 200ms | 500ms |
| 1 shipment, 200 lines, 5,000 balance rows | < 800ms | 1,500ms |
| 20 concurrent allocations same warehouse | P95 < 1,000ms, no deadlock | 3,000ms |

**Index bắt buộc:**

```sql
CREATE INDEX idx_inv_bal_alloc
  ON "InventoryBalances" ("tenantId", "warehouseId", "itemId", "inventoryStatus")
  WHERE "inventoryStatus" = 'available';
```

---

## 10. Test Cases

| TC | Input | Expected | Covers |
|---|---|---|---|
| TC-01 | Đủ tồn, FEFO | 100% allocated, lot gần hết hạn được chọn | Happy path FEFO |
| TC-02 | Đủ tồn, FIFO | 100% allocated, lot nhập sớm nhất | Happy path FIFO |
| TC-03 | Đủ tồn, LIFO | 100% allocated, lot nhập muộn nhất | Happy path LIFO |
| TC-04 | Item không có expiry, FEFO | Fallback FIFO, không lỗi | Tie-break fallback |
| TC-05 | Thiếu tồn, allowPartial=false | Error insufficientQty, ROLLBACK | Partial=false |
| TC-06 | Thiếu tồn, allowPartial=true | Partial allocated, OperationalException SHORTAGE | Partial=true |
| TC-07 | 2 luồng concurrent, same balance rows | Không deadlock, 1 thành công, 1 retry | Deadlock prevention |
| TC-08 | Lot đang QC hold giữa chừng | Lot bị bỏ qua, tìm lot thay thế | QC status guard |
| TC-09 | Duplicate call cùng idempotency-key | Trả kết quả cũ, không tạo reservation mới | Idempotency |
| TC-10 | Reservation expired sau TTL | Background job giải phóng qtyReserved | TTL cleanup |
| TC-11 | Lock timeout (NOWAIT) | Trả lockTimeout error, client retry | Lock fail-fast |
| TC-12 | Manual release shipment | Tất cả reservation -> released, qtyReserved hoàn trả | Cancel flow |

---

## 11. Integration với Rule Engine (Phase 11)

Rule Engine inject vào allocation qua `lotFilterRules`:

```json
{
  "lotFilterRules": [
    { "type": "prefer_zone", "zoneId": "ZONE-COOL" },
    { "type": "exclude_lot", "lotId": "lot_quarantine_001" },
    { "type": "strategy_override", "strategy": "FEFO" }
  ]
}
```

Allocation service apply rules trước bước sort candidates (Step 2). Rule Engine là optional.
