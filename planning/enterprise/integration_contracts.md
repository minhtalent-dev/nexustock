# Integration contracts

## Local agent message envelope

```json
{
  "messageId": "msg_001",
  "stationId": "station_pack_01",
  "deviceId": "scale_01",
  "deviceType": "scaleCom",
  "eventType": "scale.weightChanged",
  "timestamp": "2026-07-01T09:00:00Z",
  "traceId": "trc_001",
  "payload": {}
}
```

## Scale COM contract

Priority device: scale COM.

Config fields:

| Field | Example | Rule |
|---|---|---|
| portName | COM3 | Required |
| baudRate | 9600 | Device-specific |
| parity | none | none/even/odd |
| dataBits | 8 | Usually 7 or 8 |
| stopBits | 1 | Usually 1 |
| readMode | stableWindow | Required |
| stableWindowMs | 800 | Must be > 0 |
| stableTolerance | 0.02 | Decimal |
| unit | kg | kg/g/lb |

Stable reading rule:

- Parse raw frame.
- Reject negative or zero if workflow requires positive.
- Require N readings within tolerance during stable window.
- Emit `stable=true` only after stable window passes.
- Manual override requires permission, reason and audit.

## Printer contract

Priority printers:

1. Zebra ZPL.
2. TSC TSPL.

Print job fields:

| Field | Rule |
|---|---|
| printJobId | Unique per tenant |
| printerCode | Required |
| language | zpl or tspl |
| templateCode | Required active template |
| payload | Template variables only |
| copies | 1-10 by default |
| idempotencyKey | Required |

Reprint rule:

- Reprint requires original print job, reason code and permission.
- Reprint creates new job linked to original job.
- Raw label command must not contain unvalidated free text.

## Scanner keyboard wedge

- Scanner acts as keyboard input.
- UI field must auto-focus in RF/mobile screens.
- Scan must always be interpreted in workflow context.
- Unknown scan type returns clear error and does not mutate data.

## ERP payload contract

- Every inbound external message requires `externalSystem`, `externalReference`, `contractVersion`, `idempotencyKey`.
- Invalid mapping fails preview and does not commit.
- Duplicate idempotency key returns original result when payload hash matches.
- Duplicate idempotency key with different payload hash returns conflict.

## Webhook contract

Headers:

| Header | Rule |
|---|---|
| X-Nexustock-Event | Required |
| X-Nexustock-Delivery-Id | Required |
| X-Nexustock-Timestamp | Required |
| X-Nexustock-Signature | HMAC SHA-256 |
| Idempotency-Key | Required |

Retry policy:

- Retry on timeout, network error, 429, 5xx.
- Do not retry on 2xx.
- Do not retry on 400/401/403 unless manually replayed after config fix.
- Backoff: 1m, 5m, 15m, 1h, 6h.
- After max retry, move to dead letter.
