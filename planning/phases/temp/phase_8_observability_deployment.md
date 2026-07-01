# PHASE 8: OBSERVABILITY, AUDIT, KPI & DEPLOYMENT

Phase này bổ sung khả năng quan sát vận hành, audit, dashboard KPI, alert và đóng gói triển khai production.

---

## 1. Mục tiêu

* Truy vết được mọi thao tác quan trọng.
* Đo được hiệu suất vận hành kho theo thời gian thực.
* Cảnh báo sớm lỗi tồn kho, thiết bị, đồng bộ và quy trình.
* Đóng gói production bằng Docker với rollback rõ ràng.

---

## 2. Audit log

Ghi nhận toàn bộ thao tác quan trọng:

* Đăng nhập, đăng xuất, đổi quyền.
* Tạo/sửa master data quan trọng.
* Nhập, xuất, chuyển vị trí, điều chỉnh tồn.
* Hold/release QC.
* Duyệt exception.
* In tem, in lại tem.
* Nhập cân tay và lý do ghi đè.

---

## 3. Activity timeline

Timeline cần có cho:

* Lot.
* LPN.
* Serial.
* Phiếu nhập.
* Phiếu xuất.
* Vị trí kho.
* Exception.

---

## 4. Dashboard KPI

| KPI | Ý nghĩa |
|---|---|
| Inventory accuracy | Độ chính xác tồn kho |
| Open exceptions | Ngoại lệ chưa đóng |
| Pending QC | Lot chờ QC |
| Picking productivity | Năng suất picking |
| Packing throughput | Sản lượng đóng gói |
| Device health | Tình trạng cân, máy quét, máy in |
| Integration failures | Lỗi đồng bộ |

---

## 5. Alert

* Tồn dưới min hoặc vượt max.
* Lot sắp hết hạn.
* Vị trí quá tải hoặc bị khóa lâu.
* Cân, máy in, Local Agent mất kết nối.
* Đồng bộ ERP/Webhook lỗi nhiều lần.
* Exception nghiêm trọng chưa xử lý quá SLA.

---

## 6. Trace ID

* Mỗi request API có `traceId`.
* Flow scan, job nền, webhook và integration event dùng cùng trace ID khi liên quan.
* Log phải tìm được toàn bộ chuỗi xử lý theo trace ID.

---

## 7. Deployment

* Multi-stage Dockerfile build Frontend và Backend.
* Docker Compose production cho app, PostgreSQL và Redis nếu cần.
* Cấu hình fallback route cho SPA.
* Health check endpoint cho API và database.
* Rollback bằng image tag ổn định trước đó.

---

## 8. Tiêu chí hoàn tất

* Có audit log cho toàn bộ thao tác thay đổi dữ liệu quan trọng.
* Dashboard KPI hiển thị được dữ liệu vận hành chính.
* Alert hoạt động theo ngưỡng cấu hình.
* Trace ID truy vết được từ UI/API/job/integration.
* Production compose có health check và rollback plan.
