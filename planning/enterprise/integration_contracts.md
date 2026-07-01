# Integration Contracts - Nexustock WMS

Tài liệu định nghĩa các giao thức và cấu trúc dữ liệu tích hợp (máy trạm Local Agent, cân điện tử COM, máy in Zebra/TSC và webhook).

---

## 1. Cấu trúc Message trạm Local Agent

Đóng vai trò làm Envelope bọc ngoài cho mọi tin nhắn gửi qua WebSocket Secure (`wss://127.0.0.1:9000`):

```json
{
  "messageId": "msg_01H7YZZ5...",
  "stationId": "station_pack_01",
  "deviceId": "scale_com3",
  "deviceType": "scaleCom",
  "eventType": "scale.weightChanged",
  "timestamp": "2026-07-01T09:30:00Z",
  "traceId": "trc_01hxyz",
  "payload": {
    "weight": 12.35,
    "unit": "kg",
    "stable": true
  }
}
```

---

## 2. Giao thức Thiết bị Ngoại vi (Device Protocol Contracts)

### 2.1 Cân điện tử cổng COM (RS-232)
- **Cấu hình trạm:**
  - `portName`: COM3 (hoặc port được HĐH gán).
  - `baudRate`: 9600.
  - `stableWindowMs`: 800.
  - `stableTolerance`: 0.02 (kg).
- **Thuật toán xác thực số cân ổn định:**
  - Agent đọc liên tiếp luồng dữ liệu từ cổng COM.
  - Số cân chỉ được phát (`stable = true`) khi sự sai lệch giữa $N$ lần đọc liên tiếp trong khoảng thời gian `stableWindowMs` nhỏ hơn `stableTolerance`.
  - Manual override (nhập tay) bắt buộc yêu cầu quyền `print.execute` và ghi nhận mã lý do `REASON-COM-FAIL`.

### 2.2 Máy in nhãn (Zebra ZPL / TSC TSPL)
Mỗi Print Job gửi xuống Agent phải tuân thủ schema:

```json
{
  "printJobId": "job_01H7...",
  "printerCode": "PRN-ZEBRA-01",
  "language": "zpl",
  "templateCode": "LABEL-PALLET-01",
  "payload": {
    "lotNo": "LOT-MILK-001",
    "itemCode": "ITEM-001",
    "qty": "100",
    "uomCode": "BOX"
  },
  "copies": 1,
  "idempotencyKey": "idem_job_11928"
}
```

**Cơ chế in lại (Reprint):**
- In lại tem nhãn bắt buộc sinh `printJobId` mới có trường liên kết `originalPrintJobId`.
- Ghi nhận Audit Log kèm theo `reasonCode` (ví dụ: `REASON-LABEL-TORN` - Rách tem).

---

## 3. Webhook Contract & Retry Policy
Xem chi tiết cấu trúc payload và chữ ký HMAC trong [api_contracts_core.md](file:///d:/1_Project/48_Nexustock/planning/enterprise/api_contracts_core.md).

- **Retry Policy:**
  - Áp dụng Retry cho các lỗi mạng hoặc HTTP Status `5xx`, `429`.
  - Bỏ qua không retry cho lỗi Client `400`, `401`, `403` (được đưa vào hàng chờ DLQ để Admin xử lý thủ công).
  - Khoảng thời gian Retry tăng dần (Exponential Backoff): 1 phút, 5 phút, 15 phút, 1 giờ, 6 giờ.
