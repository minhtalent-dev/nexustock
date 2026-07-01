# API Contracts - Nexustock WMS Core

Tài liệu đặc tả các giao diện lập trình ứng dụng (API Contracts) cốt lõi của hệ thống WMS, tuân thủ định dạng `camelCase` cho payload và chuẩn OpenAPI 3.0.

---

## 1. OpenAPI 3.0 Specification cho các APIs Cốt lõi

```yaml
openapi: 3.0.3
info:
  title: Nexustock WMS API - Core Contracts
  version: 1.0.0
paths:
  /api/inbound/orders/{orderId}/receive:
    post:
      summary: Ghi nhận nhận hàng thực tế từ PO
      security:
        - BearerAuth: []
      parameters:
        - name: orderId
          in: path
          required: true
          schema:
            type: string
        - name: Idempotency-Key
          in: header
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/InboundReceiveRequest'
      responses:
        '200':
          description: Thành công
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/InboundReceiveResponse'
        '400':
          description: Lỗi nghiệp vụ (Vượt tolerance, trùng Lot)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorEnvelope'

  /api/outbound/shipments/{shipmentId}/allocate:
    post:
      summary: Chạy Rule Engine và phân bổ tồn kho giữ hàng
      security:
        - BearerAuth: []
      parameters:
        - name: shipmentId
          in: path
          required: true
          schema:
            type: string
        - name: Idempotency-Key
          in: header
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/AllocationRequest'
      responses:
        '200':
          description: Phân bổ thành công (toàn bộ hoặc một phần)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/AllocationResponse'
        '400':
          description: Lỗi phân bổ (Hết hàng khả dụng nếu allowPartial = false)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorEnvelope'

components:
  securitySchemes:
    BearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT

  schemas:
    InboundReceiveRequest:
      type: object
      required:
        - warehouseId
        - lineId
        - itemId
        - lotNo
        - qty
        - uomCode
        - locationId
      properties:
        warehouseId:
          type: string
        lineId:
          type: string
        itemId:
          type: string
        lotNo:
          type: string
        qty:
          type: number
          format: double
        uomCode:
          type: string
        locationId:
          type: string
        expiryDate:
          type: string
          format: date
        reasonCode:
          type: string
          nullable: true

    InboundReceiveResponse:
      type: object
      properties:
        inboundOrderId:
          type: string
        status:
          type: string
        lotId:
          type: string
        transactionId:
          type: string
        receivedQty:
          type: number
          format: double
        traceId:
          type: string

    AllocationRequest:
      type: object
      required:
        - warehouseId
        - strategy
      properties:
        warehouseId:
          type: string
        strategy:
          type: string
          enum: [FEFO, FIFO, LIFO]
        allowPartial:
          type: boolean
          default: true
        reservationTtlMinutes:
          type: integer
          default: 1440

    AllocationResponse:
      type: object
      properties:
        shipmentId:
          type: string
        status:
          type: string
          enum: [allocated, partially_allocated]
        allocatedLinesCount:
          type: integer
        reservations:
          type: array
          items:
            type: object
            properties:
              reservationId:
                type: string
              itemId:
                type: string
              qty:
                type: number
                format: double
        traceId:
          type: string

    ErrorEnvelope:
      type: object
      properties:
        errorCode:
          type: string
        message:
          type: string
        details:
          type: object
        traceId:
          type: string
```

---

## 2. API Tích hợp Webhook & Chữ ký HMAC

### 2.1 Cấu trúc Webhook Payload (Định dạng gửi đi từ WMS)
Khi có sự kiện xuất kho (`outbound.shipped`), WMS phát tin nhắn webhook:

```json
{
  "eventId": "evt_01H7YZZ...",
  "eventType": "outbound.shipped",
  "timestamp": "2026-07-01T09:30:00Z",
  "tenantId": "tnt_vinamilk_01",
  "traceId": "trc_01hxyz",
  "payload": {
    "shipmentId": "shp_001",
    "shipmentNo": "SO-20260701-99",
    "carrierCode": "INTERNAL",
    "trackingNo": "TRK-998822",
    "shippedLines": [
      {
        "itemId": "item_milk_opt",
        "lotNo": "LOT-MILK-001",
        "qty": 100.0,
        "uomCode": "BOX"
      }
    ]
  }
}
```

### 2.2 Thuật toán ký HMAC SHA-256 trên Webhook Header
Mỗi webhook payload gửi đi được mã hóa chữ ký trong Header `X-Nexustock-Signature`. 

Cấu thức code backend tạo chữ ký:
```csharp
using System.Security.Cryptography;
using System.Text;

public static class WebhookSigner
{
    public static string CreateSignature(string payloadJson, string secretKey, string timestamp)
    {
        var encoding = new UTF8Encoding();
        var signatureString = $"{timestamp}.{payloadJson}";
        byte[] keyByte = encoding.GetBytes(secretKey);
        byte[] messageBytes = encoding.GetBytes(signatureString);
        
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            return Convert.ToHexString(hashmessage).ToLower();
        }
    }
}
```
*Giao thức bắt tay bảo mật:* Đối tác nhận webhook bắt buộc phải tự tính lại chữ ký trên và so sánh với giá trị header `X-Nexustock-Signature` để đảm bảo dữ liệu không bị giả mạo trên đường truyền.

---

## 3. API Versioning Strategy

### 3.1 Chiến lược phiên bản (URL-based versioning)

Nexustock WMS sử dụng **URL path versioning** — phiên bản nhúng vào path, không dùng header versioning hay query param versioning.

```
/api/v1/inbound/orders
/api/v1/outbound/shipments
/api/v1/inventory/balances
/api/v2/inbound/orders   -- khi có breaking change
```

**Lý do chọn URL-based:** Dễ debug (Postman, curl, browser), dễ đọc log, tương thích với mọi HTTP client không cần thêm header.

### 3.2 Quy tắc backward compatibility

Các thay đổi **KHÔNG cần bump version** (additive-only, backward compatible):
- Thêm field mới vào response JSON (client cũ bỏ qua)
- Thêm query parameter mới (optional, có default)
- Thêm endpoint mới hoàn toàn
- Mở rộng enum value (thêm value mới)

Các thay đổi **BẮT BUỘC bump version** (breaking changes):
- Đổi tên field trong request/response
- Xóa field bắt buộc
- Thay đổi kiểu dữ liệu field
- Thay đổi HTTP method của endpoint
- Thay đổi nghĩa của error code

### 3.3 Deprecation policy

```
v1 (current) --> v2 ra mắt
  |
  +--> v1 tiếp tục hoạt động song song TỐI THIỂU 30 ngày
  |
  +--> Header cảnh báo: Deprecation: "Sun, 01 Aug 2026 00:00:00 GMT"
  |                     Sunset: "Sun, 31 Aug 2026 00:00:00 GMT"
  |
  +--> Sau 30 ngày: v1 trả 410 Gone với message migration guide
```

**Chính sách hỗ trợ đa phiên bản:** Tối đa 2 version hoạt động đồng thời. Khi v3 ra, v1 phải được sunset.

### 3.4 Routing configuration (ASP.NET Core)

```csharp
// Program.cs
app.MapControllerRoute(
    name: "v1",
    pattern: "api/v1/{controller}/{action=Index}/{id?}");

// Controller attribute
[ApiController]
[Route("api/v1/[controller]")]
public class InboundOrdersController : ControllerBase { }

// Version 2 khi cần
[ApiController]
[Route("api/v2/[controller]")]
public class InboundOrdersV2Controller : ControllerBase { }
```

### 3.5 ERP integration versioning

API dùng cho ERP integration (Phase 23) phải pin version trong config:

```json
{
  "ErpIntegration": {
    "WmsApiBaseUrl": "https://wms.nexustock.io/api/v1",
    "ApiVersion": "v1"
  }
}
```

Khi bump version, SAP team phải được thông báo trước 30 ngày và contract validation test phải pass trên version mới trước khi sunset version cũ.
