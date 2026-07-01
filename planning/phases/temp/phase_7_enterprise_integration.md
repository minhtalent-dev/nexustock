# PHASE 7: HARDWARE, ERP/WMS LEGACY & API INTEGRATION

Phase này chuẩn hóa lớp tích hợp giữa Nexustock với phần cứng, hệ thống cũ, ERP, import/export và webhook.

---

## 1. Mục tiêu

* Tách integration layer khỏi core nghiệp vụ.
* Kết nối Local Agent, cân điện tử, máy quét, máy in nhãn.
* Chuẩn hóa API contract với ERP/WMS legacy.
* Có integration log, retry policy, idempotency key và trạng thái đồng bộ.

---

## 2. Local Agent & phần cứng

* Windows Worker Service đọc COM port, file scan, cân điện tử và máy in.
* WebSocket Server nội bộ `ws://localhost:9000` đẩy trạng thái thiết bị lên Web UI.
* Raw printing ZPL/TSPL cho máy in nhãn.
* Health check thiết bị: scanner, scale, printer, agent service.

---

## 3. API contract enterprise

| Contract | Dữ liệu |
|---|---|
| Inbound order | PO/Invoice, vendor, item, expected qty |
| Outbound order | Shipment, customer, item, requested qty |
| Inventory balance | Item, Lot, LPN, location, qty |
| Stock adjustment | Lý do, số lượng lệch, người duyệt |
| Item master | Item, UOM, package, tracking policy |
| Partner master | Vendor, customer, carrier, plant |

---

## 4. Import/export

* CSV/Excel import cho Item, UOM, Location, Partner, opening stock.
* Export inventory balance, transaction history, exception list và KPI vận hành.
* Mỗi import có preview, validate, commit và rollback report.

---

## 5. Webhook & integration event

| Event | Khi phát sinh |
|---|---|
| `inbound.completed` | Nhận hàng xong |
| `qc.completed` | QC xong |
| `shipment.completed` | Xuất hàng xong |
| `inventory.adjusted` | Điều chỉnh tồn kho |
| `exception.created` | Phát sinh ngoại lệ |

---

## 6. Độ tin cậy tích hợp

* Mọi message có `idempotencyKey`.
* Retry theo backoff, có giới hạn số lần.
* Dead-letter queue cho message lỗi kéo dài.
* Integration log lưu request, response, status, retry count và trace ID.
* EDI để phase sau, chỉ triển khai khi đối tác yêu cầu thực tế.

---

## 7. Tiêu chí hoàn tất

* Local Agent đọc cân, scan và in tem ổn định.
* API contract đủ cho ERP/WMS legacy đồng bộ dữ liệu chính.
* Import/export có validate và rollback report.
* Webhook có retry, idempotency và integration log.
