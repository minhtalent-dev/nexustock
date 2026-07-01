# PHASE 10: TESTING, HARDENING & PRODUCTION ACCEPTANCE

Phase này chuẩn hóa kiểm thử, hardening, cutover, rollback và nghiệm thu để đưa Nexustock vào vận hành production an toàn.

---

## 1. Mục tiêu

* Kiểm thử toàn bộ core WMS, RF/mobile, exception, rule engine, integration và observability.
* Đảm bảo không âm kho, không lệch tồn do race condition và không bypass quyền trái phép.
* Có checklist cutover, rollback plan và tiêu chí nghiệm thu rõ ràng.

---

## 2. Tầng kiểm thử

| Tầng | Công cụ | Phạm vi |
|---|---|---|
| Unit | xUnit, Jest | Logic rule, FIFO/FEFO, validation, permission helper |
| Integration | ASP.NET Core Test, PostgreSQL test DB | API, transaction, RBAC, concurrency, integration retry |
| E2E | Playwright | Flow scan, QC, picking, packing, exception, dashboard |
| Hardware simulation | Local Agent simulator | Cân, scan, in tem, mất kết nối |
| UAT | Checklist nghiệp vụ | Người vận hành kiểm thử theo ca/kho |

---

## 3. Kịch bản bắt buộc

* Nhận hàng -> QC -> cất hàng.
* Chuyển vị trí bằng Lot và LPN.
* Kiểm kê, khóa vị trí, phê duyệt chênh lệch.
* Picking theo FIFO/FEFO và xử lý bypass có quyền.
* Packing có cân điện tử và fallback cân tay.
* Exception sai mã, sai Lot, sai vị trí, thiếu hàng, dư hàng.
* Rule putaway, allocation, picking, replenishment.
* LPN, Serial, RMA, Wave Picking và Genealogy.
* Webhook retry, idempotency và integration log.
* Dashboard KPI, audit log và trace ID.

---

## 4. Hardening

* Validate input tại toàn bộ trust boundary.
* Chặn âm kho bằng transaction và concurrency token.
* Bắt buộc auth cho mọi API thay đổi dữ liệu.
* Mask dữ liệu nhạy cảm trong log.
* Backup database trước cutover.
* Kiểm tra restore backup trên môi trường staging.

---

## 5. Cutover checklist

1. Freeze thay đổi dữ liệu trên hệ thống cũ.
2. Backup dữ liệu hệ thống cũ.
3. Chạy migration dữ liệu sang PostgreSQL.
4. Đối soát Item, Lot, Location, Inventory balance.
5. Chạy smoke test flow nhập, xuất, kiểm kê, in tem, cân.
6. Mở quyền người dùng production.
7. Theo dõi dashboard và exception trong ca đầu.

---

## 6. Rollback plan

* Giữ hệ thống cũ ở trạng thái read-only trong giai đoạn đầu.
* Nếu lỗi nghiêm trọng, dừng Nexustock, xuất transaction phát sinh, đối soát và quay về hệ thống cũ.
* Rollback app bằng Docker image tag ổn định.
* Restore database từ backup nếu dữ liệu bị sai lệch nghiêm trọng.

---

## 7. Acceptance criteria

| Tiêu chí | Chuẩn đạt |
|---|---|
| Core flow | Nhập, QC, cất, xuất, kiểm kê chạy đúng |
| Inventory accuracy | Không âm kho, không lệch sau concurrency test |
| Security | 100% API thay đổi dữ liệu có auth và permission |
| RF/mobile | Flow scan chính chạy được trên handheld/mobile viewport |
| Exception | Lỗi vận hành tạo exception và xử lý được |
| Integration | Retry, idempotency, log và alert hoạt động |
| Observability | Audit, KPI, trace ID truy vết được |
| Rollback | Có phương án quay lui đã diễn tập |

---

## 8. Tiêu chí hoàn tất

* Toàn bộ test critical pass.
* UAT được người vận hành xác nhận.
* Cutover checklist hoàn tất.
* Rollback plan đã diễn tập.
* Dashboard production không có alert nghiêm trọng trước go-live.
