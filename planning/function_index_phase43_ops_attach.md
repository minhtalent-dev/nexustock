# Function Index — Phase 43 Core Ops Attach + Master Spreadsheet

**Date:** 2026-07-23 · **Workflow:** `rp2` /17-auto-plan  
**SoT:** `planning/phases/phase_43_ops_attachments_spreadsheet.md` (§22 `rp1` · §23 `rp2`)  
**Upstream:** P41+P42 **ĐÓNG** · Files Hub + `EntityAttachmentsPanel`  
**Maturity:** **100% Ready** (giữ sau `rp2`)

---

## 0. Bản đồ hệ thống (hiện trạng)

| Layer | Path / Artifact | Vai trò P43 |
|---|---|---|
| Files | `AttachmentService` allowlist PRODUCT/QC_RESULT | **EXTEND** allowlist 6 + existence |
| Files | `EntityAttachmentsPanel` | **REUSE** C·R·D + URL — **MUST NOT** fork |
| Files csproj | MasterData + Inventory | **GIỮ** — **MUST NOT** ref Inbound/Rma/Qc |
| Api | `DatabaseSeeder` ExtraPermissions | **ADD** `ops.export` |
| Api | Controllers / DI | **NEW** OpsExports + ExistenceHandlers |
| MasterData | `ImportService` / `ExportsController` | **EXTEND** +4 types |
| FE master | uoms/warehouses/zones/reasons + import | **EXTEND** ExportButtons + dropdown |
| FE ops | inbound receive · outbound · stocktake · rma · qc | **WIRE** panel |
| MUST NOT | Lot/Exception/LPN/Wave attach · P45 line/RF · P42 migrate | → P44/P45 / giữ |

---

## 1. Function catalog (F01–F36)

### Foundation / DI / Allowlist

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F01 | Interface `IEntityExistenceHandler` | EP0 | `Files/Services/IEntityExistenceHandler.cs` | `CanHandle` · `ExistsAsync` |
| F02 | `AttachmentService` inject `IEnumerable<IEntityExistenceHandler>` | EP0–EP1 | `AttachmentService.cs` | Fallback inline MasterData/Inventory |
| F03 | Allowlist +6 constants | EP1 | `AttachmentService.cs` | PRODUCT·QC_RESULT·INBOUND_ORDER·SHIPMENT·STOCKTAKE·RMA_REQUEST |
| F04 | Inline exists PRODUCT | EP1 | same | Đã có — giữ |
| F05 | Inline exists SHIPMENT / STOCKTAKE | EP1 | same | Via `InventoryDbContext` (Files đã ref) |
| F06 | Handler `InboundOrderExistenceHandler` | EP1 | `Api/ExistenceHandlers/` | `InboundDbContext.InboundOrders` |
| F07 | Handler `RmaRequestExistenceHandler` | EP1 | same | `RmaDbContext` |
| F08 | Handler `QcResultExistenceHandler` | EP1 | same | `QcDbContext.QcResults` — **bắt buộc** (hiện QC_RESULT không validate) |
| F09 | Register handlers trong Api DI | EP1 | `Program.cs` hoặc `Files`+Api extension | Scoped |
| F10 | Error `ENTITY_TYPE_NOT_ALLOWED` / `ATTACHMENT_ENTITY_NOT_FOUND` | EP1 | reuse `FileDomainException` | 400 / 404 |

### Permissions / Ops export API

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F11 | Seed `ops.export` | EP0 | `DatabaseSeeder.cs` ExtraPermissions | Admin + WM theo pattern seeder |
| F12 | `OpsExportsController` GET | EP4 | `Api/Controllers/OpsExportsController.cs` | `type` + `format` csv\|xlsx |
| F13 | Export INBOUND_ORDERS | EP4 | same + InboundDbContext | Cột §7.2 khóa rp1 |
| F14 | Export SHIPMENTS | EP4 | InventoryDbContext | |
| F15 | Export STOCKTAKES | EP4 | Inventory — `zoneId` · `totalVarianceAmount` | **không** warehouseCode |
| F16 | Export RMA | EP4 | RmaDbContext | |
| F17 | Cap 5000 + `OPS_EXPORT_*` errors | EP4 | controller | Reuse ClosedXML via MasterData `SpreadsheetReader` hoặc copy helper |
| F18 | Authz `ops.export` | EP4 | controller | 403 |

### Master Import / Export

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F19 | Template + preview/commit UOMS | EP3 | `ImportService.cs` | `code,name,isActive` |
| F20 | Template + preview/commit WAREHOUSES | EP3 | same | `code,name,description,isActive` — **không** address |
| F21 | Template + preview/commit ZONES | EP3 | same | `StorageZone` · `warehouseCode,code,name,zoneType` |
| F22 | Template + preview/commit REASONS | EP3 | same | `code,reasonType,description,isActive` |
| F23 | `ExportsController` +4 Build*Async | EP3 | `ExportsController.cs` | Same 4 types |
| F24 | Unique code per tenant validation | EP3 | ImportService | Mirror ITEMS pattern |

### Frontend

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F25 | `MasterDataExportButtons` type union +4 | EP3 | `export-buttons.tsx` | UOMS\|WAREHOUSES\|ZONES\|REASONS |
| F26 | Wire ExportButtons 4 master pages | EP3 | `uoms/warehouses/zones/reasons/page.tsx` | |
| F27 | Import dropdown +4 options + i18n | EP3 | `master-data/import/page.tsx` + messages | |
| F28 | Panel Inbound receive | EP2 | `inbound/[id]/receive/page.tsx` | `INBOUND_ORDER` + order id |
| F29 | Panel Outbound detail | EP2 | `outbound/page.tsx` | Trong `selectedShipment` pane |
| F30 | Panel Stocktake detail | EP2 | `stocktakes/[id]/page.tsx` | `STOCKTAKE` |
| F31 | Panel RMA detail | EP2 | `rma/page.tsx` | `selectedRma` |
| F32 | Panel QC dialog | EP2 | `qc-result-dialog.tsx` | Panel khi có resultId · giữ compat `attachmentRefs` |
| F33 | Ops Export buttons 4 list pages | EP4 | inbound/outbound/stocktakes/rma pages | csv\|xlsx gọi `/ops-exports` |
| F34 | i18n keys EN+VI | EP2–EP4 | `messages/{en,vi}/` | MasterData.import.options + Admin ops export |

### Verify / MUST NOT

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F35 | `tests/verify_ops_attach_p43.ps1` | EP5 | `tests/` | Gates §22.5 |
| F36 | **MUST NOT** | ALL | — | P44 attach Lot/Exc/LPN/Wave · P45 line/RF · Files→Qc ProjectReference · đổi P42 migrate · fork panel |

---

## 2. Trace EP ↔ F

| EP | Goal | F-ids | Validation |
|---|---|---|---|
| EP0 | Interface + seed stub + evidence | F01, F11 | build |
| EP1 | Allowlist 6 + handlers + bind 404/201 | F02–F10, F09 | curl bind fake →404 · WAVE→400 |
| EP2 | FE 5 panels | F28–F32, F34 | dbm smoke 5 màn |
| EP3 | Master IE 4 | F19–F27 | preview UOMS xlsx · export ZONES |
| EP4 | OpsExports + FE buttons | F12–F18, F33 | download 4 types · 403 |
| EP5 | verify + docs + plan | F35 | verify PASS · F36 checklist · plan row |

---

## 3. Luồng runtime

```text
Upload: POST /api/files/upload → storage
Bind:   POST /api/files/attachments { entityType, entityId, ... }
        → allowlist? → handler/inline Exists? → insert file_attachments
List:   GET  /api/files/attachments?entityType&entityId → URLs (open/download)
Delete: DELETE /api/files/attachments/{id} soft + object best-effort

Master: POST /api/imports/preview?type=UOMS|… → commit
        GET  /api/exports?type=UOMS&format=xlsx

Ops:    GET  /api/ops-exports?type=SHIPMENTS&format=csv  (ops.export)
```

### Pseudo bind (khóa)

```csharp
var type = request.EntityType.Trim().ToUpperInvariant();
if (!Allowed.Contains(type)) throw ENTITY_TYPE_NOT_ALLOWED;
var ok = type switch {
  "PRODUCT" => await _md.Products.AnyAsync(...),
  "SHIPMENT" => await _inv.Shipments.AnyAsync(...),
  "STOCKTAKE" => await _inv.Stocktakes.AnyAsync(...),
  _ => await _handlers.First(h => h.CanHandle(type)).ExistsAsync(request.EntityId, ct)
};
if (!ok) throw ATTACHMENT_ENTITY_NOT_FOUND;
```

---

## 4. MUST NOT (executor)

1. Không thêm ProjectReference Files → Inbound/Rma/Qc.  
2. Không implement Lot/Exception/LPN/Wave/Putaway/XD attach (P44).  
3. Không ASN line import / RF / thumb (P45).  
4. Không fork `EntityAttachmentsPanel`.  
5. Không đổi contract migrate P42 / OpenRead.  
6. Không dùng cột `address` cho Warehouse import.  
7. Không phá verify_files_spreadsheet / verify_storage_migrate.

---

## 5. Verdict `rp2`

| Mục | Giá trị |
|---|---|
| F-map | F01–F36 |
| Critic | ≥9.0 (xem brain critic_report) |
| Blockers `/18` | **0** |
| JARVIS | **`rp2` PASS** · sẵn sàng `rp3` hoặc Proceed `/18` |
