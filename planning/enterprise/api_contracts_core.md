# API contracts core

## Shared rules

- JSON uses camelCase.
- Mutation requires auth, permission and `Idempotency-Key` header.
- Every response includes `traceId`.
- List APIs use `page`, `pageSize`, `sort`, `filters`; max `pageSize` = 100.

## Error envelope

```json
{
  "errorCode": "inventory.insufficientAvailableQty",
  "message": "Available quantity is not enough.",
  "details": { "itemCode": "ITEM-001", "requestedQty": 10, "availableQty": 6 },
  "traceId": "trc_01hxyz"
}
```

## Inbound receive

`POST /api/inbound/orders/{orderId}/receive`

Permission: `inbound_receiving.receive`

```json
{
  "warehouseId": "wh_001",
  "lineId": "iol_001",
  "itemId": "item_001",
  "lotNo": "LOT-20260701-001",
  "qty": 12.5,
  "uomCode": "PCS",
  "locationId": "loc_receiving_01",
  "expiryDate": "2027-07-01",
  "reasonCode": null
}
```

Success:

```json
{
  "inboundOrderId": "inb_001",
  "status": "receiving",
  "lotId": "lot_001",
  "transactionId": "txn_001",
  "receivedQty": 12.5,
  "traceId": "trc_01hxyz"
}
```

## Inventory move

`POST /api/inventory/moves`

Permission: `inventory_movement.create`

```json
{
  "warehouseId": "wh_001",
  "itemId": "item_001",
  "lotId": "lot_001",
  "fromLocationId": "loc_a01",
  "toLocationId": "loc_b01",
  "qty": 5,
  "uomCode": "PCS",
  "reasonCode": "MOVE_NORMAL"
}
```

## Allocate shipment

`POST /api/outbound/shipments/{shipmentId}/allocate`

Permission: `allocation_reservation.create`

```json
{
  "warehouseId": "wh_001",
  "strategy": "FEFO",
  "allowPartial": false,
  "reservationTtlMinutes": 1440
}
```

## Pick task confirm

`POST /api/picking/tasks/{taskId}/confirm`

Permission: `outbound_picking.confirm`

```json
{
  "scannedLocationCode": "A-01-01",
  "scannedItemCode": "ITEM-001",
  "scannedLotNo": "LOT-20260701-001",
  "pickedQty": 5,
  "exceptionCode": null
}
```

## Pack and ship

`POST /api/packing/sessions/{sessionId}/close`

```json
{
  "cartonNo": "CTN-0001",
  "weight": 12.35,
  "weightSource": "scaleCom",
  "labelJobId": "prn_001"
}
```

`POST /api/outbound/shipments/{shipmentId}/ship`

```json
{
  "carrierCode": "INTERNAL",
  "trackingNo": "TRK-001",
  "shipTime": "2026-07-01T09:00:00Z"
}
```

## Exception create

`POST /api/exceptions`

```json
{
  "warehouseId": "wh_001",
  "sourceType": "pickTask",
  "sourceId": "pick_001",
  "exceptionCode": "SHORT_PICK",
  "severity": "medium",
  "description": "Short quantity found at location.",
  "evidence": { "scannedLocationCode": "A-01-01" }
}
```
