# Domain state machines

## Global rules

- State values are stable enum strings, not free text.
- Every transition writes actor, timestamp, reason code when required, and trace ID.
- Invalid transition returns conflict, not silent correction.
- Completed, cancelled, rejected, voided states are terminal unless document states reopen path.
- `tenantId` is mandatory for every workflow entity; `warehouseId` is mandatory when operation happens inside warehouse.

## InboundOrder

```mermaid
stateDiagram-v2
    [*] --> draft
    draft --> open: submit
    open --> receiving: startReceive
    receiving --> partiallyReceived: receivePartial
    receiving --> completed: receiveFull
    partiallyReceived --> receiving: continueReceive
    partiallyReceived --> completed: closeShortWithApproval
    draft --> cancelled: cancel
    open --> cancelled: cancel
    receiving --> exception: ruleBreak
    exception --> receiving: resolve
    completed --> [*]
    cancelled --> [*]
```

## Lot

```mermaid
stateDiagram-v2
    [*] --> created
    created --> qcPending: receive
    qcPending --> released: passQc
    qcPending --> hold: hold
    hold --> released: release
    hold --> rejected: reject
    released --> allocated: reserve
    allocated --> picked: pick
    picked --> shipped: ship
    rejected --> scrap: dispose
```

## InventoryBalance

```mermaid
stateDiagram-v2
    [*] --> available
    available --> reserved: allocate
    reserved --> picked: pick
    picked --> packed: pack
    packed --> shipped: ship
    available --> hold: qcHold
    hold --> available: release
    available --> counted: cycleCountLock
    counted --> available: postAdjustment
```

## InventoryTransaction

- Allowed types: RECEIVE, MOVE, HOLD, RELEASE, RESERVE, UNRESERVE, PICK, PACK, SHIP, COUNT_ADJUST, RETURN, SCRAP.
- Transaction rows are append-only.
- Wrong transaction is corrected by compensating transaction, never update/delete.
- Required fields: `tenantId`, `warehouseId`, `itemId`, `locationId`, `lotId`, `lpnId`, `qty`, `uomId`, `transactionType`, `sourceType`, `sourceId`, `traceId`.

## Shipment

```mermaid
stateDiagram-v2
    [*] --> draft
    draft --> open: releaseToWarehouse
    open --> allocated: allocate
    allocated --> picking: startPick
    picking --> picked: completePick
    picked --> packing: startPack
    packing --> packed: closePack
    packed --> shipped: confirmShip
    open --> cancelled: cancel
    allocated --> cancelled: cancelAndUnreserve
```

## PickTask

```mermaid
stateDiagram-v2
    [*] --> open
    open --> assigned: assignWorker
    assigned --> inProgress: scanStart
    inProgress --> completed: confirmPick
    inProgress --> shortPicked: reportShort
    shortPicked --> exception: createException
    exception --> open: replan
    open --> cancelled: cancel
```

## PackSession

```mermaid
stateDiagram-v2
    [*] --> open
    open --> weighing: captureWeight
    weighing --> labelled: printLabel
    labelled --> closed: closeCarton
    open --> exception: printerOrScaleError
    exception --> open: resolve
```

## AllocationReservation

```mermaid
stateDiagram-v2
    [*] --> active
    active --> consumed: pick
    active --> released: unreserve
    active --> expired: ttlExpired
    active --> reallocated: replan
```

## OperationalException

```mermaid
stateDiagram-v2
    [*] --> open
    open --> assigned: assignOwner
    assigned --> inProgress: investigate
    inProgress --> resolved: resolve
    resolved --> closed: verify
    open --> cancelled: duplicateOrInvalid
```

## WebhookDelivery

```mermaid
stateDiagram-v2
    [*] --> pending
    pending --> sending: dispatch
    sending --> delivered: success2xx
    sending --> retryScheduled: timeoutOr5xx
    retryScheduled --> sending: backoffElapsed
    retryScheduled --> deadLetter: retryLimitExceeded
    deadLetter --> replayQueued: manualReplay
    replayQueued --> sending: replay
```

## PrintJob

```mermaid
stateDiagram-v2
    [*] --> queued
    queued --> sending: agentAccepted
    sending --> printed: printerAck
    sending --> failed: timeoutOrOffline
    failed --> queued: retry
    printed --> reprintRequested: reasonApproved
    reprintRequested --> queued: enqueueReprint
```

## DeviceSession

```mermaid
stateDiagram-v2
    [*] --> unpaired
    unpaired --> pairingRequested: operatorStart
    pairingRequested --> paired: tokenVerified
    paired --> connected: websocketOpen
    connected --> degraded: heartbeatMissed
    degraded --> connected: heartbeatRecovered
    degraded --> disconnected: timeout
    connected --> revoked: adminRevoke
```
