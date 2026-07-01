# PHASE 5: RULE ENGINE, SLOTTING, ALLOCATION & REPLENISHMENT

Phase này tách luật vận hành kho khỏi code cứng để hệ thống dễ đổi theo nhà máy, zone, nhóm hàng, khách hàng và chính sách tồn kho.

---

## 1. Mục tiêu

* Chuẩn hóa rule putaway, allocation, picking, replenishment, FEFO/FIFO và zone constraint.
* Bắt đầu bằng rule cấu hình dạng bảng, chưa cần DSL riêng.
* Rule phải giải thích được vì sao hệ thống đề xuất hoặc chặn thao tác.

---

## 2. Nhóm rule bắt buộc

| Nhóm rule | Mục đích |
|---|---|
| Putaway | Đề xuất vị trí cất hàng phù hợp |
| Allocation | Giữ hàng cho đơn xuất theo ưu tiên |
| Picking | Chọn Lot/LPN/Serial để lấy hàng |
| Replenishment | Bổ sung hàng từ reserve location sang pick face |
| FEFO/FIFO | Kiểm soát thứ tự xuất theo hạn dùng hoặc ngày sản xuất |
| Zone constraint | Chặn hàng sai vùng bảo quản, vùng khóa, vùng cách ly |

---

## 3. Thiết kế dữ liệu rule tối thiểu

| Bảng | Mục đích |
|---|---|
| `RuleSets` | Nhóm rule theo nghiệp vụ và tenant |
| `RuleConditions` | Điều kiện áp dụng rule |
| `RuleActions` | Hành động đề xuất, chặn hoặc cảnh báo |
| `RulePriorities` | Thứ tự ưu tiên xử lý |
| `RuleExecutionLogs` | Log lý do rule match hoặc không match |

---

## 4. Putaway & slotting

* Lọc vị trí theo warehouse, zone, trạng thái khóa và trạng thái cách ly.
* Kiểm tra capacity, max volume, kích thước và trọng lượng.
* Ưu tiên vị trí theo rotation speed, weight class, temperature constraint và proximity.
* Trả về tối đa 3 vị trí khuyên dùng kèm lý do.

---

## 5. Allocation & picking

* Giữ hàng theo shipment, priority, customer, Lot, QC status và expiry date.
* Không allocation hàng đang hold, đang kiểm kê, đang cách ly hoặc đã reserve cho đơn khác.
* Picking mặc định theo FEFO/FIFO, cho phép bypass nếu có quyền quản lý và reason code.

---

## 6. Replenishment

* Theo dõi pick face min/max.
* Khi tồn pick face dưới ngưỡng, tạo replenishment task từ reserve location.
* Không tạo trùng task nếu task cũ còn mở.
* Ưu tiên bổ sung hàng có vận tốc luân chuyển cao.

---

## 7. API cần có

| API | Mục đích |
|---|---|
| `POST /api/rules/evaluate` | Chạy rule theo context |
| `GET /api/putaway/proposals` | Lấy vị trí cất hàng đề xuất |
| `POST /api/allocation/reserve` | Giữ hàng cho đơn xuất |
| `POST /api/picking/validate` | Kiểm tra Lot/LPN được pick |
| `POST /api/replenishment/tasks/generate` | Sinh nhiệm vụ bổ sung hàng |

---

## 8. Tiêu chí hoàn tất

* Rule engine xử lý được putaway, allocation, picking, replenishment, FEFO/FIFO và zone constraint.
* Mỗi đề xuất hoặc lỗi chặn có lý do rõ ràng.
* Có integration test cho rule conflict và priority.
* ponytail: Chưa xây DSL rule riêng. Nâng cấp khi số rule vượt 100 hoặc cần non-developer tự cấu hình phức tạp.
