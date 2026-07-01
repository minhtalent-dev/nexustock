# ERP mock payloads (Mẫu dữ liệu tích hợp ERP với WMS)

Tài liệu cung cấp các mẫu payload JSON chính thức cho kết nối tích hợp giữa hệ thống ERP downstream và Nexustock WMS.

---

## 1. Đồng bộ Đơn nhập kho (Inbound Purchase Order)

- **Chi tiết:** ERP truyền đơn PO này sang WMS khi nhà cung cấp chuẩn bị giao hàng.
- **API Endpoint trên WMS:** `POST /api/integration/inbound-orders`
- **Yêu cầu an toàn:** Bắt buộc Header `Idempotency-Key` và chữ ký `X-Nexustock-Signature`.

### Mock Payload:

```json
{
  "integrationHeader": {
    "externalSystem": "SAP-ERP",
    "externalReference": "PO-2026-99881",
    "contractVersion": "v1.0",
    "idempotencyKey": "idem_po_99881_20260701_001",
    "timestamp": "2026-07-01T09:00:00Z"
  },
  "inboundOrder": {
    "tenantId": "tenant_nexustock_demo",
    "warehouseCode": "wh_hn_01",
    "orderNo": "PO-2026-99881",
    "partnerCode": "SUPPLIER-MILK-VN",
    "partnerName": "Nhà cung cấp sữa Việt Nam",
    "orderDate": "2026-07-01",
    "expectedArrivalDate": "2026-07-03",
    "note": "Hàng dễ vỡ, yêu cầu cất kho lạnh ngay sau khi QC",
    "items": [
      {
        "lineNo": 1,
        "itemCode": "MILK-DRY-900",
        "itemName": "Sữa bột Optimum 900g",
        "expectedQty": 120.000000,
        "uomCode": "LON",
        "tolerancePct": 5.00
      },
      {
        "lineNo": 2,
        "itemCode": "MILK-FRSH-180",
        "itemName": "Sữa tươi tiệt trùng 180ml",
        "expectedQty": 480.000000,
        "uomCode": "HOP",
        "tolerancePct": 0.00
      }
    ]
  }
}
```

---

## 2. Đồng bộ Đơn xuất kho (Outbound Sales Order / Shipment)

- **Chi tiết:** ERP truyền đơn xuất kho này sang WMS khi có đơn hàng cần giao cho khách hàng hoặc chuyển kho nội bộ.
- **API Endpoint trên WMS:** `POST /api/integration/outbound-orders`

### Mock Payload:

```json
{
  "integrationHeader": {
    "externalSystem": "SAP-ERP",
    "externalReference": "SO-2026-11223",
    "contractVersion": "v1.0",
    "idempotencyKey": "idem_so_11223_20260701_002",
    "timestamp": "2026-07-01T09:05:00Z"
  },
  "outboundOrder": {
    "tenantId": "tenant_nexustock_demo",
    "warehouseCode": "wh_hn_01",
    "shipmentNo": "SO-2026-11223",
    "partnerCode": "COOP-MART-HN",
    "partnerName": "Siêu thị Co.opmart Hà Nội",
    "shipToAddress": "123 Trần Hưng Đạo, Hoàn Kiếm, Hà Nội",
    "priority": 2,
    "requiredDeliveryDate": "2026-07-02T17:00:00Z",
    "note": "Giao trước 5h chiều tránh tắc đường",
    "lines": [
      {
        "lineNo": 1,
        "itemCode": "MILK-DRY-900",
        "requestedQty": 24.000000,
        "uomCode": "LON"
      },
      {
        "lineNo": 2,
        "itemCode": "MILK-FRSH-180",
        "requestedQty": 96.000000,
        "uomCode": "HOP"
      }
    ]
  }
}
```

---

## 3. Webhook Báo cáo Xác nhận Xuất kho hoàn tất (Shipment Confirmation Webhook)

- **Chi tiết:** WMS phát đi webhook này sang ERP ngay sau khi thủ kho xác nhận xe hàng đã rời kho thành công để ERP tự động trừ kho kế toán và xuất hóa đơn.
- **Phương thức nhận:** ERP cung cấp endpoint nhận Webhook, WMS đóng vai trò Client gọi đi.

### Mock Payload:

```json
{
  "webhookHeader": {
    "event": "shipment.confirmed",
    "deliveryId": "dlv_ship_001hxy762",
    "timestamp": "2026-07-01T15:30:22Z",
    "signature": "87e35b7194f4c28f9d6c7ee3c85dae44b94f6bb2d354b38dcd2b7b25867bc581"
  },
  "payload": {
    "tenantId": "tenant_nexustock_demo",
    "warehouseCode": "wh_hn_01",
    "shipmentNo": "SO-2026-11223",
    "status": "shipped",
    "carrierCode": "VIETTEL-POST",
    "trackingNo": "VTP-9988221",
    "shippedAt": "2026-07-01T15:29:45Z",
    "confirmedBy": "packer_01",
    "traceId": "trc_out_001hxyz",
    "details": [
      {
        "lineNo": 1,
        "itemCode": "MILK-DRY-900",
        "requestedQty": 24.000000,
        "shippedQty": 24.000000,
        "lots": [
          {
            "lotNo": "LOT-26A-01",
            "qty": 24.000000
          }
        ]
      },
      {
        "lineNo": 2,
        "itemCode": "MILK-FRSH-180",
        "requestedQty": 96.000000,
        "shippedQty": 96.000000,
        "lots": [
          {
            "lotNo": "LOT-26F-01",
            "qty": 96.000000
          }
        ]
      }
    ]
  }
}
```
