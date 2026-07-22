# Nexustock Function Acceptance Review

## 1. Phạm vi nghiệm thu

Tài liệu này rà soát trực tiếp logic mã nguồn Nexustock theo từng chức năng tương ứng trong 3 hệ tham chiếu:

- GCM Part: vật tư, lot, IQC, tồn kho sản xuất.
- GCM Shipping: gom chuyến, pallet/package set, đăng ký xuất, hủy đăng ký xuất, FIFO theo package set.
- warehouse-main: WMS Laravel/Filament có workflow, audit, serial/hybrid, transfer, export, adjustment, movement.

Mục tiêu: kết luận Nexustock đã đúng và đủ logic nghiệp vụ production WMS chưa. Không dùng đánh giá mơ hồ; mỗi gap bên dưới gắn với logic đã đọc.

---

## 2. Kết luận nghiệm thu tổng

| Nhóm chức năng | Điểm | Trạng thái | Kết luận nghiệm thu |
|---|---:|---|---|
| Master Data | 72/100 | Cần bổ sung | Đủ nền master WMS, thiếu tracking method `quantity/serial/hybrid`, access scope theo warehouse/team, audit/recycle sâu như warehouse-main. |
| Inbound Receiving | 78/100 | Nghiệm thu có điều kiện | Có order, lot, tolerance, capacity guard, transaction; thiếu approval workflow chuẩn, UOM conversion, serial/hybrid inbound end-to-end. |
| Lot & IQC/QC Gate | 70/100 | Cần bổ sung | Có QC gate chặn lot chưa Release; thiếu bad quantity, IQC count, sample plan engine, unit conversion, trạng thái in/issue lot như GCM Part. |
| Inventory Balance | 76/100 | Nghiệm thu có điều kiện | Có QtyOnHand/QtyReserved/transaction/row version; thiếu invariant tập trung chặn âm kho và reserved âm. |
| Allocation | 82/100 | Đạt khá | Có FIFO/QC/location lock ở controller, có AllocationService mạnh hơn; nhưng tồn tại 2 luồng allocation gây lệch rule. |
| Outbound Pick-Pack-Ship | 74/100 | Cần bổ sung | Có shipment, generate pick, complete pick, pack weight; thiếu ship/handover cuối, multi-package allocation, serial issue trong outbound. |
| Serial/LPN | 68/100 | Cần bổ sung | LPN có create/attach/detach/move/event; thiếu lock liên DB transaction chuẩn, thiếu nối LPN vào outbound pack/ship, thiếu serial lifecycle tương đương warehouse-main. |
| Warehouse Layout/Location | 75/100 | Nghiệm thu có điều kiện | Có zone/location/capacity/location lock; thiếu access scope và recycle/audit location như warehouse-main. |
| Transfer/Adjustment/Stocktake | 72/100 | Cần bổ sung | Move/adjust/stocktake có transaction, lock vị trí kiểm kê, approval theo cấp; thiếu transfer document lifecycle và movement kép tương đương warehouse-main. |
| Wave Picking | 76/100 | Nghiệm thu có điều kiện | Có wave + slot map + allocation service theo index trước; cần loại allocation cũ trong OutboundController. |
| Audit/Approval/Security | 69/100 | Cần bổ sung | Có permission và traceId/CreatedBy; thiếu HasWorkflowApproval/audit/activity/recycle thống nhất như warehouse-main. |
| UI/UX & Reporting | 70/100 | Cần bổ sung | Có frontend module theo index trước; cần nghiệm thu browser bằng dữ liệu thật sau khi đóng P0 nghiệp vụ. |

**Điểm tổng:** 73.0/100  
**Trạng thái tổng:** Nexustock đủ nền WMS kỹ thuật, chưa đủ khóa production toàn hệ thống trước khi đóng P0/P1.

---

## 3. Ma trận đối chiếu 4 chiều

| Nhóm | Nexustock evidence | GCM Part evidence | GCM Shipping evidence | warehouse-main evidence | Kết luận |
|---|---|---|---|---|---|
| Master Data | `Product`, `Uom`, `Warehouse`, `StorageLocation` có tenant, code/name, base UOM, serial flag, capacity. | Part/lot/unit legacy trong `DBIF_Parts`, `DBIF_CPiece`. | Shipping dùng package/product nội bộ qua package set. | `Product`, `Warehouse`, `StorageLocation`, `Inventory` có tracking method, audit, recycle, scope. | Nexustock đúng khung, thiếu depth quản trị. |
| Inbound | `InboundController.ReceiveItem`, `InventoryService.RecordReceiptAsync`, `Lot`. | `frm113_Iqc_Input`, `T_PART_OUTER_LOT_INFO` có maker lot, valid date, bad quantity, IQC count. | Không phải core inbound shipping. | `StockImport.complete()` transaction + serial data + movement. | Nexustock đủ nhận lượng thường; serial/IQC chưa đủ. |
| QC/Lot gate | `QcGateService` chặn lot không Release. | `WCHECK_FLAG`, `BAD_STOCK_FLAG`, `IQC_COUNT`, `BAD_QUANTITY`. | FIFO shipping dùng package set status, không thay QC. | Workflow/audit hỗ trợ duyệt trạng thái. | Gate đúng, nội dung IQC thiếu. |
| Inventory | `MoveInventory`, `AdjustInventory`, `CompletePick`, `InventoryTransaction`. | `SetQuantity`, `UpdateQuantity` cập nhật quantity và event. | Shipment registration đổi status package/shipment. | `Inventory.available_quantity`, `StockMovement`, `StockAdjustment`. | Có ledger, thiếu invariant chung. |
| Allocation/FIFO | `GeneratePicks` lọc lot Release, location lock, order by `LotNo`. | Lot/valid date dùng trong tồn sản xuất. | `OrganizeShipmentSet` kiểm package set cũ hơn theo `SET_DATE`. | Export kiểm available quantity, không có FEFO service riêng. | Nexustock tự động hơn GCM, nhưng FIFO key chưa cùng bản chất GCM. |
| Outbound | `CreateShipment`, `GeneratePicks`, `CompletePick`, `CompletePacking`. | Không phải core. | `frm104` gom shipment set; `frm107` đăng ký xuất `S`, cancel về `A`, event P011/P012. | `StockExport.complete()` authorized → completed, trừ tồn, serial issue, movement. | Nexustock thiếu bước ship/issue cuối và serial xuất. |
| LPN/Serial | `LpnService` split inventory, attach/detach/move, LpnEvent. | Legacy lot nhiều hơn serial. | Package set/pallet tương đương container logic. | `StockExport`/`StockTransfer` xử lý serial_data, `ProductSerial` status available/issued. | LPN có nền; serial end-to-end thiếu. |
| Transfer | `MoveInventory` chuyển location trong kho theo item/lot/location. | `UpdateQuantity` giảm used/bad. | Không có transfer kho rõ trong form đã đọc. | `StockTransfer` draft/pending/approved/in_transit/completed, ship/confirm, movement in/out, serial transfer. | Nexustock move là movement vật lý, chưa là transfer document. |
| Adjustment/Stocktake | `AdjustInventory`, `StocktakeController.ApproveStocktake`. | Bad/used quantity trong outer lot. | Không phải core. | `StockAdjustment` approval, reason, movement, audit/recycle. | Stocktake khá tốt; manual adjustment thiếu workflow document. |
| Audit/Security | Permission string, CreatedBy, TraceId, RowVersion. | Event history P011/P012, worker check. | Event history shipment. | HasWorkflowApproval, LogsActivity, Auditable, Comments, Recyclable. | Nexustock chưa đạt audit workflow chuẩn warehouse-main. |

---

## 4. Đối chiếu chi tiết: Outbound và FIFO

### 4.1 GCM Shipping: gom chuyến và FIFO package set

Evidence:

- `D:\1_Project\2_GCM\2_GCM_Shipping\GCM_PART\frm104_Fprd_OrganizeShipmentSet.vb`
- `D:\1_Project\2_GCM\2_GCM_Shipping\GCM_PART\frm107_Fprd_ShipmentRegistration.vb`
- `D:\1_Project\2_GCM\2_GCM_Shipping\GCM_PART\frm107_Fprd_ShipmentRegistration_Cancel.vb`

Logic đã đọc:

1. Form gom chuyến lấy package set từ bảng package set/package member/shipment package set.
2. FIFO không dựa trên lot text. FIFO dựa trên `SET_DATE` của `T_FPRD_PACKAGE_SET_INFORMATION`.
3. Khi chọn package set xuất, hệ thống tìm package set cùng `INTERNAL_PRODUCT` có `SET_DATE` cũ hơn và chưa xuất (`STATUS` không phải `O`).
4. Nếu tồn tại package set cũ hơn, form cảnh báo FIFO mở ra. Tùy `FIFO_Mode_sw`, người dùng bị chặn hoặc phải xác nhận.
5. Đăng ký xuất ở `frm107_Fprd_ShipmentRegistration.DoOK()`:
   - Kiểm tra worker không rỗng.
   - Kiểm tra có dòng invoice/package được chọn.
   - Xác nhận thao tác.
   - Trong `TransactionScope`, từng dòng chọn gọi `ShipInfo.UpdateStatus("S", ShipmentID)`.
   - Ghi event history `P011` kèm invoice, shipment date, destination, shipment ID, worker.
6. Hủy đăng ký xuất ở `frm107_Fprd_ShipmentRegistration_Cancel.DoOK()`:
   - Kiểm tra worker.
   - Kiểm tra có dòng chọn.
   - Xác nhận thao tác.
   - Trong `TransactionScope`, từng dòng chọn gọi `ShipInfo.UpdateStatus("A", ShipmentID)`.
   - Ghi event history `P012`.

Kết luận GCM Shipping:

- GCM có trạng thái xuất rõ: `A` trước xuất, `S` đã đăng ký xuất, package set có trạng thái xuất riêng.
- GCM có event history bắt buộc theo worker.
- GCM có FIFO theo ngày đóng package thực tế, không theo chuỗi lot.

### 4.2 Nexustock: outbound hiện tại

Evidence:

- `D:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.Inventory\Controllers\OutboundController.cs`

Logic đã đọc:

1. `CreateShipment`:
   - Kiểm permission `Outbound.Shipments.Create`.
   - Kiểm partner tồn tại theo tenant.
   - Kiểm product tồn tại theo tenant.
   - Kiểm trùng `ShipmentNo`.
   - Tạo `Shipment` status `Open`.
   - Tạo `ShipmentItem` với `RequestedQty`, `PickedQty = 0`, `PackedQty = 0`.
2. `GeneratePicks`:
   - Chỉ chạy khi shipment status `Open`.
   - Lấy inventory theo `ItemId`, `TenantId`, `QtyOnHand - QtyReserved > 0`.
   - Sort FIFO bằng `OrderBy(i => i.LotNo)`.
   - Lấy lot release từ `Lots` với `QcStatus == "Release"`.
   - Loại location lock `OUTBOUND` hoặc `ALL`.
   - Kiểm tổng khả dụng đủ requested qty.
   - Tăng `QtyReserved` trên inventory.
   - Tạo `PickTask` status `Pending`.
   - Đổi shipment status `Allocated`.
3. `CompletePick`:
   - Chỉ xử lý pick status `Pending`.
   - Kiểm `PickedQty > 0` và `PickedQty <= pickTask.Qty`.
   - Gọi QC gate lại bằng `EnsureLotUsableByLotNoAsync`.
   - Lấy inventory đúng item/lot/location.
   - Chỉ kiểm `QtyOnHand >= PickedQty`.
   - Trừ `QtyOnHand -= PickedQty` và `QtyReserved -= PickedQty`.
   - Ghi `InventoryTransaction` type `PICK_OUT`.
   - Cập nhật `PickTask` completed và cộng `ShipmentItem.PickedQty`.
   - Nếu all pick completed, đổi shipment status `Picking`.
4. `CompletePacking`:
   - Cho status `Picking` hoặc `Allocated`.
   - Validate weight bằng `WeightValidationService`.
   - Tạo `PackingRecord` status `Completed`.
   - Gán `PackedQty = PickedQty` cho mọi shipment item.
   - Đổi shipment status `Packed`.

Đối chiếu trực tiếp:

| Điểm | GCM Shipping | Nexustock | Kết luận |
|---|---|---|---|
| FIFO | Theo `SET_DATE` package set cũ hơn cùng product. | Theo `LotNo` dạng chuỗi. | Không tương đương hoàn toàn. Nếu `LotNo` không encode thời gian chuẩn, FIFO sai. |
| Cảnh báo FIFO | Có form cảnh báo/chặn tùy config. | Backend tự phân bổ, không cho chọn sai FIFO. | Nexustock tốt hơn về tự động, nhưng thiếu proof FIFO theo ngày thực tế. |
| Đăng ký xuất | Status `S`, event `P011`. | Sau pack status `Packed`, chưa thấy endpoint ship/handover. | Nexustock thiếu bước đăng ký xuất cuối tương đương `S`. |
| Hủy đăng ký | Status về `A`, event `P012`. | Chưa thấy cancel/reverse pick/pack trong evidence. | Gap P1/P0 tùy vận hành. |
| Event history | Bắt buộc worker. | Có `CreatedBy`, `TraceId`, transaction. | Đủ audit cơ bản, thiếu event domain chuẩn cho shipment. |
| Package/Pallet | GCM package set/pallet/carton count. | `PackingRecord` một record/package weight, packed qty toàn dòng. | Chưa đủ multi-package/pallet mapping. |

Nghiệm thu Outbound/FIFO: **74/100**.

Gap bắt buộc:

- P0: `CompletePick` phải kiểm thêm `QtyReserved >= PickedQty` trước khi trừ reserved.
- P0: FIFO phải dùng ngày nhập/ngày lot/ngày sản xuất/hạn dùng chuẩn, không dùng `LotNo` nếu không có format bảo đảm.
- P1: Thêm endpoint ship/handover/cancel shipment có event domain tương đương `P011/P012`.
- P1: Packing phải cho nhiều package/pallet, map item/qty vào từng package.

---

## 5. Đối chiếu chi tiết: warehouse-main StockExport

Evidence:

- `D:\1_Project\warehouse-main\warehouse-main\src\app\Models\StockExport.php`
- `D:\1_Project\warehouse-main\warehouse-main\src\app\Models\StockExportItem.php`
- `D:\1_Project\warehouse-main\warehouse-main\src\app\Models\Inventory.php`
- `D:\1_Project\warehouse-main\warehouse-main\src\app\Models\ProductSerial.php`

Logic warehouse-main đã đọc:

1. `StockExport` dùng trait `HasWorkflowApproval`, `LogsActivity`, `HasComments`, `Recyclable`, `Auditable`.
2. Status gồm `draft`, `pending`, `authorized`, `completed`, `cancelled`, `returned`, `overdue`.
3. `canBeAuthorized()` yêu cầu status `pending`, có item, và `hasAvailableStock()` trả true.
4. `hasAvailableStock()`:
   - Với quantity item: kiểm `Inventory.available_quantity >= item.quantity` theo product/warehouse/condition.
   - Với serial_data: lọc serial hợp lệ, kiểm `ProductSerial` status `available` trong warehouse.
5. `complete()`:
   - Chỉ cho status `authorized`.
   - Gọi `validateNoDuplicateSerials()`.
   - Gọi `validateSerialsAvailable()`.
   - Trong DB transaction, lock inventory bằng `lockForUpdate()`.
   - Với quantity product: kiểm available, kiểm location quantity nếu có, decrement location và inventory.
   - Với serial/hybrid: tính valid serial count, kiểm available, decrement location từng serial, decrement inventory theo count.
   - Tạo `StockMovement` type `out`, source `sale`, `quantity_before`, `quantity_moved`, `quantity_after`.
   - Update `ProductSerial` sang `issued`, ghi `exported_at`, `exported_by`, `export_reference_no`, recipient, destination.
   - Update export status `completed`.
6. `StockExportItem`:
   - Có `serial_data` array.
   - Validate quantity > 0 cho product không phải serial.

Đối chiếu Nexustock:

| Điểm | warehouse-main | Nexustock | Kết luận |
|---|---|---|---|
| Workflow | pending → authorized → completed. | Open → Allocated → Picking → Packed. | Nexustock thiếu bước authorized/approval trước xuất. |
| Lock tồn kho | `lockForUpdate()` khi complete. | `GeneratePicks` không lock row rõ; `CompletePick` không lock row rõ. | Nexustock có RowVersion nhưng thiếu lock pessimistic ở luồng cũ. |
| Serial outbound | Duplicate check, availability check, update serial issued. | OutboundController chưa xử lý serial. | Gap lớn nếu sản phẩm serial. |
| Location qty | Kiểm location quantity riêng. | Inventory theo LocationId nên có kiểm dòng location. | Nexustock đạt tương đương ở quantity theo location. |
| Movement | `StockMovement` có before/moved/after. | `InventoryTransaction` có type/qty/traceId, không có before/after. | Nexustock audit ledger ít thông tin hơn. |
| Loan/return | Có loan, due, overdue, returned. | Không thấy trong outbound evidence. | Không cần nếu WMS sản xuất, cần nếu kho công cụ/tài sản. |

Nghiệm thu so với warehouse-main Export: **71/100**.

---

## 6. Đối chiếu chi tiết: LPN và Serial

### 6.1 Nexustock LPN

Evidence:

- `D:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.Lpn\Services\LpnService.cs`

Logic đã đọc:

1. `CreateLpnAsync`:
   - Kiểm storage location tồn tại.
   - Kiểm trùng `LpnNo` theo tenant, case-insensitive.
   - Tạo LPN status `ACTIVE`.
   - Ghi `LpnEvent` type `CREATE`.
2. `AttachToLpnAsync`:
   - Mở transaction trên `_dbContext` LPN.
   - Kiểm LPN tồn tại và status `ACTIVE`.
   - Gọi QC gate lot usable.
   - Tìm inventory tự do tại location của LPN: `LpnId == null`.
   - Kiểm `QtyOnHand - QtyReserved >= dto.Qty`.
   - Nếu source lớn hơn qty attach: split dòng inventory, chia reserved theo tỷ lệ, dòng mới gắn `LpnId`.
   - Nếu attach toàn bộ: gán `sourceInv.LpnId = lpn.Id`.
   - Ghi `LpnEvent` type `ATTACH`.
   - Save LPN context và inventory context.
3. `DetachFromLpnAsync`:
   - Mở transaction trên `_dbContext`.
   - Tìm inventory thuộc LPN theo item/lot.
   - Kiểm `QtyOnHand >= dto.Qty`.
   - Nếu detach một phần: split dòng, chia reserved theo tỷ lệ, dòng mới `LpnId = null`.
   - Nếu detach toàn bộ: gán `sourceInv.LpnId = null`.
   - Ghi `LpnEvent` type `DETACH`.
4. `MoveLpnAsync`:
   - Kiểm LPN tồn tại.
   - Nếu target trùng old location thì return true.
   - Kiểm target location tồn tại.
   - Lấy tất cả inventory thuộc LPN.
   - Gọi QC gate cho từng lot.
   - Cập nhật `lpn.LocationId` và `inventory.LocationId`.
   - Ghi `InventoryMovement` reason `LPN_MOVE` cho từng inventory.
   - Ghi `LpnEvent` type `MOVE`.

Kết luận:

- Nexustock LPN có logic container hóa tồn kho tốt hơn warehouse-main ở phần LPN vì warehouse-main không có LPN chuyên biệt trong evidence đã đọc.
- Lỗi kiến trúc: transaction chỉ mở trên `_dbContext` nhưng ghi cả `_inventoryContext`. Nếu 2 DbContext khác connection/transaction, rollback không bảo đảm atomic toàn bộ.
- Lỗi rule: `DetachFromLpnAsync` không kiểm available qty khi tách reserved; chỉ kiểm `QtyOnHand`. Có thể tách hàng đang reserved ra khỏi LPN.
- Lỗi rule: split reserved theo tỷ lệ khi attach/detach có thể tạo reserved trên dòng mới dù thao tác attach/detach thường chỉ nên với qty available hoặc phải giữ reservation identity.

### 6.2 warehouse-main Serial

Evidence:

- `StockExport.complete()` xử lý serial outbound.
- `StockTransfer.moveSerialItem()` xử lý serial transfer.
- `ProductSerial` status `available`, `issued`, warehouse/location, condition.

Đối chiếu:

| Điểm | warehouse-main | Nexustock | Kết luận |
|---|---|---|---|
| Serial status | `available` → `issued`, có exported metadata. | Chưa thấy OutboundController update serial. | Nexustock thiếu serial outbound lifecycle. |
| Serial transfer | Lock serial `available`, chuyển warehouse/location. | LPN không xử lý serial ID, chỉ item/lot/qty. | Nexustock LPN không thay thế serial tracking. |
| Duplicate serial | Export validate duplicate serial. | Chưa thấy trong outbound. | Gap P0 nếu hàng serial. |
| LPN event | warehouse-main không có LPN event chuyên biệt. | Có LpnEvent. | Nexustock mạnh hơn phần container. |

Nghiệm thu Serial/LPN: **68/100**.

---

## 7. Đối chiếu chi tiết: Transfer, Adjustment, Stocktake

### 7.1 warehouse-main StockTransfer

Evidence:

- `D:\1_Project\warehouse-main\warehouse-main\src\app\Models\StockTransfer.php`

Logic đã đọc:

1. Status: `draft`, `pending`, `approved`, `in_transit`, `completed`, `cancelled`.
2. `canBeApproved()` yêu cầu status `pending`, có item, `hasAvailableStock()` true.
3. `hasAvailableStock()`:
   - Với serial: từng serial phải tồn tại trong warehouse nguồn, status `available`, `canBeTransferred()` true; số serial hợp lệ phải đủ quantity.
   - Với quantity: inventory product/warehouse/condition phải đủ available quantity.
   - Nếu có source location, location quantity phải đủ.
4. `ship()`:
   - Chỉ chạy khi approved.
   - Legacy quantity mode trừ inventory nguồn và tạo movement `out` source `transfer`.
   - Serial mode gọi process transfer.
   - Status thành `in_transit`.
5. `confirm()`:
   - Chỉ chạy khi `approved` hoặc `in_transit`.
   - `completeSerialTransfer()` trong transaction.
   - Status thành `completed`, ghi confirmer/time.
6. `moveQuantityItem()`:
   - Lock inventory nguồn bằng `lockForUpdate()`.
   - Kiểm available quantity.
   - Lock source location nếu có.
   - Trừ source inventory nếu khác warehouse.
   - Tăng target location và target inventory.
   - Tạo 2 movement: out ở warehouse nguồn, in ở warehouse đích.
7. `moveSerialItem()`:
   - Lọc serial hợp lệ.
   - Lock serial ở source warehouse status `available`.
   - Lock inventory nguồn.
   - Trừ source location/inventory.
   - Tăng target location/inventory.
   - Update serial warehouse/location.
   - Tạo movement cho từng serial.

### 7.2 Nexustock MoveInventory

Evidence:

- `D:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.Inventory\Controllers\InventoryController.cs`

Logic đã đọc:

1. Kiểm permission `Inventory.Movements.Create`.
2. Gọi QC gate `EnsureLotUsableByLotNoAsync`.
3. Kiểm source và target location tồn tại.
4. Kiểm source không bị lock `OUTBOUND` hoặc `ALL`.
5. Kiểm target không bị lock `INBOUND` hoặc `ALL`.
6. Tìm inventory theo tenant/item/lot/fromLocation.
7. Kiểm `QtyOnHand - QtyReserved >= dto.Qty`.
8. Kiểm capacity target: tổng QtyOnHand tại target + dto.Qty <= MaxCapacity.
9. Trong transaction:
   - Trừ source `QtyOnHand`.
   - Xóa source nếu onhand/reserved = 0.
   - Tìm target inventory cùng tenant/item/lot/toLocation.
   - Nếu có thì cộng target QtyOnHand; nếu không tạo dòng mới.
   - Ghi `InventoryMovement` status `Completed`, reason code từ request.
   - Ghi 2 `InventoryTransaction`: `MOVE_OUT` âm và `MOVE_IN` dương.

Đối chiếu:

| Điểm | warehouse-main Transfer | Nexustock MoveInventory | Kết luận |
|---|---|---|---|
| Document workflow | Có phiếu transfer, duyệt, ship, confirm. | Endpoint move trực tiếp. | Nexustock chỉ là movement nội kho, chưa là transfer nghiệp vụ. |
| Multi-warehouse | Có from/to warehouse. | Dựa location; không thấy phiếu liên warehouse. | Thiếu transfer document nếu cần liên kho. |
| Lock row | `lockForUpdate()`. | Không thấy row lock; dựa RowVersion/concurrency. | Nên bổ sung lock hoặc retry cho high concurrency. |
| Movement ledger | Tạo out/in movement có before/after. | Tạo movement + transaction out/in, không có before/after. | Đủ trace cơ bản, thiếu before/after. |
| Serial transfer | Có serial transfer. | Không có serial trong MoveInventory. | Gap nếu hàng serial. |
| Location lock | Không thấy lock type chi tiết trong đoạn transfer. | Có lock inbound/outbound/all. | Nexustock tốt hơn về location lock. |

Nghiệm thu Transfer nội kho: **73/100** nếu chỉ yêu cầu di chuyển vị trí.  
Nghiệm thu Transfer liên kho dạng phiếu: **55/100** vì thiếu workflow document.

### 7.3 warehouse-main StockAdjustment

Evidence:

- `D:\1_Project\warehouse-main\warehouse-main\src\app\Models\StockAdjustment.php`
- `D:\1_Project\warehouse-main\warehouse-main\src\app\Models\StockMovement.php`

Logic đã đọc:

1. `StockAdjustment` dùng audit/activity/recycle/reference auto.
2. Status: `draft`, `pending`, `approved`, `completed`, `cancelled`.
3. `requiresApproval()` kiểm adjustment lớn theo quantity difference.
4. `approve()` đổi status approved, ghi approver/time/note.
5. Khi created:
   - Nếu không có item, cập nhật inventory từ `quantity_after`.
   - Nếu có item, lặp từng item, cộng/trừ inventory theo `quantity_difference`.
   - Ghi `StockMovement` cho từng adjustment.
6. `StockMovement` có `reference_no`, `quantity_before`, `quantity_moved`, `quantity_after`, `source`, `moved_at`.

### 7.4 Nexustock AdjustInventory và Stocktake

Evidence:

- `InventoryController.AdjustInventory`
- `StocktakeController.CreateStocktake`
- `StocktakeController.StartStocktake`
- `StocktakeController.RecordCount`
- `StocktakeController.ApproveStocktake`
- `StockAdjustment`, `StockAdjustmentItem`

Logic đã đọc:

1. `AdjustInventory`:
   - Permission: `exception_framework_mvp.approve` hoặc `Inventory.Movements.Create`.
   - Idempotency bằng `InventoryTransaction.TraceId == dto.IdempotencyKey`.
   - Nếu qty > 0: tạo/cộng inventory, ghi ledger `ADJ_IN`.
   - Nếu qty < 0: kiểm inventory tồn tại, kiểm available >= abs qty, trừ inventory, xóa nếu zero, ghi ledger `ADJ_OUT`.
   - Có transaction và concurrency catch.
2. `CreateStocktake`:
   - Permission `Inventory.CycleCount.Create`.
   - Check duplicate stocktake no.
   - Tạo status `Draft`.
3. `StartStocktake`:
   - Chỉ status `Draft`.
   - Lấy location theo zone hoặc toàn kho.
   - Snapshot current stock thành `StocktakeItem` với `SystemQty`.
   - Lock toàn bộ target location bằng `LocationLock` type `ALL`, reason `STOCKTAKE` nếu chưa lock.
   - Status thành `Counting`.
4. `RecordCount`:
   - Chỉ status `Counting`.
   - Tìm item theo location/item/lot.
   - Nếu chưa có, tạo item system qty 0.
   - Set counted qty, variance qty, status `Counted`.
5. `ApproveStocktake`:
   - Nếu status `Counting`: chặn pending item, tính `TotalVarianceAmount` bằng `Abs(variance) * 500000m`, set pending L1/L2/L3 theo ngưỡng.
   - Nếu pending: kiểm permission theo cấp duyệt.
   - Trong transaction, nếu có variance tạo `StockAdjustment` status `Applied` và `StockAdjustmentItem`.
   - Với variance dương: tạo/cộng inventory, ledger `ADJ_IN`.
   - Với variance âm: kiểm inventory, available, trừ inventory, ledger `ADJ_OUT`.
   - Xóa location locks reason `STOCKTAKE`.
   - Status stocktake `Approved`.

Đối chiếu:

| Điểm | warehouse-main | Nexustock | Kết luận |
|---|---|---|---|
| Manual adjustment | Document + approval + movement. | Direct endpoint adjust + idempotency. | Nexustock nhanh, thiếu workflow document cho manual adjustment. |
| Stocktake | Không thấy stocktake sâu trong evidence. | Có stocktake snapshot, lock location, count, approve L1/L2/L3, apply adjustment. | Nexustock mạnh ở kiểm kê. |
| Approval | `requiresApproval()` theo chênh lệch lớn. | Stocktake approval theo tổng variance; direct adjust bypass nếu user có permission. | Stocktake tốt, direct adjust cần workflow/gate. |
| Audit | LogsActivity/Auditable/Recyclable. | Transaction + CreatedBy + TraceId. | Thiếu audit/activity/comment/recycle. |
| Movement detail | before/moved/after. | adjustment item có before/after/delta; transaction chỉ qty. | Stocktake item đủ, ledger thiếu before/after. |
| Negative stock | Một số nơi warehouse-main dùng max(0), không phải tuyệt đối tốt. | Nexustock kiểm available khi giảm. | Nexustock tốt hơn direct giảm tồn. |

Nghiệm thu Adjustment/Stocktake: **72/100**.

---

## 8. Gap P0/P1 đã xác định rõ

### P0 - Chặn trước production

| Gap | Evidence | Rủi ro | Hành động bắt buộc |
|---|---|---|---|
| Reserved âm khi complete pick | `CompletePick` chỉ kiểm `QtyOnHand < PickedQty`, sau đó `QtyReserved -= PickedQty`. | Sai available stock, âm reserved, allocation lệch. | Thêm guard `inventory.QtyReserved >= dto.PickedQty`; thêm invariant tại DbContext/DB. |
| FIFO key chưa đúng bản chất GCM | GCM FIFO dùng `SET_DATE`; Nexustock dùng `LotNo`. | FIFO sai nếu LotNo không phản ánh ngày nhập/sản xuất/hạn dùng. | Chuyển allocation sang `ReceivedAt`/`ManufacturedAt`/`ExpiryDate`/Lot created date; rule rõ FIFO/FEFO. |
| Hai luồng allocation | `GeneratePicks` tự allocate; hệ có `AllocationService` riêng theo index trước. | QC/location/FIFO/retry lệch giữa wave và outbound. | Outbound chỉ gọi `AllocationService`; xóa logic cũ hoặc bọc adapter. |
| Serial outbound chưa có | warehouse-main `StockExport.complete()` update serial `issued`; Nexustock outbound không thấy serial. | Xuất hàng serial không có lifecycle, trùng/thiếu serial không bị chặn. | Thêm serial scan/validate/issue vào pick/pack/ship. |
| Cross-DbContext transaction LPN chưa atomic | `LpnService` transaction trên `_dbContext`, save `_inventoryContext` riêng. | LPN event commit nhưng inventory rollback lệch hoặc ngược lại. | Dùng shared transaction/Outbox hoặc hợp nhất DbContext boundary. |

### P1 - Nâng chuẩn nghiệm thu

| Gap | Evidence | Hành động |
|---|---|---|
| Thiếu ship/handover cuối | Shipment dừng ở `Packed`; GCM có status `S`, event P011. | Thêm `ShipShipment`, `CancelShipment`, domain event shipment. |
| Thiếu cancel/reverse outbound | GCM cancel status `A`, event P012. | Reverse pick reservation/packing theo trạng thái. |
| Packing đa kiện/pallet chưa đủ | `CompletePacking` set toàn bộ `PackedQty = PickedQty`. | Tạo package items: packageNo/item/lot/qty/weight/pallet. |
| Manual adjustment bypass workflow | `AdjustInventory` chạy trực tiếp nếu có permission. | Đưa approval request cho adjustment lớn hoặc nhạy cảm. |
| Audit trail chưa đồng nhất | warehouse-main dùng activity/audit/comment/recycle; Nexustock dùng CreatedBy/TraceId rải rác. | Chuẩn hóa domain audit event cho shipment/stocktake/adjustment/LPN. |
| Transfer liên kho chưa có document | Nexustock MoveInventory là endpoint trực tiếp. | Thêm transfer order: Draft/Pending/Approved/InTransit/Completed/Cancelled. |
| Ledger thiếu before/after | `InventoryTransaction` chỉ có Qty. | Thêm beforeQty/afterQty hoặc snapshot event. |
| LPN detach chưa kiểm available | `DetachFromLpnAsync` chỉ kiểm QtyOnHand. | Chặn detach reserved hoặc giữ reservation mapping rõ. |
| Stocktake variance cost hard-code | `500000m`. | Lấy standard cost từ product/cost table. |

---

## 9. Nghiệm thu theo từng chức năng

| Chức năng Nexustock | Quyết định nghiệm thu | Điều kiện |
|---|---|---|
| Master Data | Nghiệm thu có điều kiện | Bổ sung tracking method, access scope, audit/recycle. |
| Inbound Receive | Nghiệm thu có điều kiện | Bổ sung serial/hybrid inbound, UOM conversion, approval workflow. |
| QC Gate | Nghiệm thu gate cơ bản | Bổ sung IQC depth: bad qty, sample plan, IQC count, print/issue. |
| Inventory Balance | Nghiệm thu có điều kiện | Thêm invariant DB/DbContext cho on-hand/reserved. |
| Move Inventory | Nghiệm thu movement nội kho | Nếu cần transfer liên kho, phải bổ sung transfer document. |
| Stocktake | Nghiệm thu có điều kiện | Thay hard-code cost, audit approval, before/after ledger. |
| Manual Adjustment | Không nghiệm thu production | Cần workflow approval và audit chuẩn. |
| Allocation | Nghiệm thu sau hợp nhất | Chỉ để một `AllocationService` làm SoT. |
| Pick | Không nghiệm thu production trước P0 | Guard reserved, row lock/retry, serial pick. |
| Pack | Nghiệm thu demo | Cần package item/pallet/multi-package. |
| Ship | Không nghiệm thu | Chưa thấy endpoint ship/handover/cancel đầy đủ. |
| LPN Create/Move | Nghiệm thu có điều kiện | Fix transaction atomic, location capacity/lock target. |
| LPN Attach/Detach | Không nghiệm thu production trước P0 | Fix reserved split/detach rule, atomic transaction. |
| Serial | Không nghiệm thu | Thiếu lifecycle nhập-pick-pack-ship-transfer đầy đủ. |
| Audit/Security | Nghiệm thu cơ bản | Cần domain audit event và approval framework chung. |

---

## 10. Kết luận cuối

Nexustock đã vượt legacy GCM ở nền tảng hiện đại: tenant, permission, module hóa, QC gate, location lock, capacity, reservation, stocktake approval, LPN event. Tuy nhiên GCM Shipping và warehouse-main vẫn có những điểm nghiệp vụ production Nexustock chưa đủ:

- GCM Shipping có FIFO theo ngày package set thực tế và event xuất/hủy xuất rõ.
- warehouse-main có workflow approval, audit/activity/recycle, serial lifecycle, movement before/after, transfer document.
- Nexustock hiện thiếu khóa invariant tồn kho tập trung, thiếu serial outbound, thiếu ship/cancel cuối, thiếu transfer document, và LPN transaction chưa atomic xuyên context.

**Quyết định nghiệm thu:** Chưa nghiệm thu production toàn hệ thống. Chỉ nghiệm thu có điều kiện các phần Master Data, Inbound thường, Inventory balance, Move nội kho, Stocktake, Allocation sau khi đóng P0. Outbound production, Serial, LPN attach/detach, Transfer liên kho phải sửa trước khi go-live.
