# PHASE 46C: Master Spreadsheet Regression + Full Ops Exports

## Execution Spec

| Mục | Giá trị |
|---|---|
| Trạng thái | ⬜ Spec Ready |
| Ước lượng | 2–3 dev-days |
| Upstream | P43; P46A cho shared download utility |
| Downstream | P46E |
| Scope nguồn | P43 Master IE 4 + Ops exports 4; P44 Ops exports 8 |

## 1. Mục tiêu

Khóa và xác thực 100% spreadsheet/export scope P43–P44: 4 Master types preview/commit/export/roundtrip và đủ 12 Ops export types với schema cột ổn định.

## 2. P43 Master Import/Export Regression

| Type | Columns bắt buộc |
|---|---|
| `UOMS` | `code,name,description,isActive,errorMessage` |
| `WAREHOUSES` | `code,name,address,isActive,errorMessage` |
| `ZONES` | `warehouseCode,code,name,zoneType,isActive,errorMessage` |
| `REASONS` | `code,name,reasonType,description,isActive,errorMessage` |

Mỗi type phải pass:

1. Download template CSV/XLSX.
2. Preview valid file, không ghi DB.
3. Preview invalid file, trả row/column/message.
4. Download error CSV UTF-8 BOM.
5. Commit transaction.
6. Recommit cùng batch idempotent/409 theo contract hiện có.
7. Export CSV/XLSX.
8. Export → preview import roundtrip.
9. Tenant isolation và permission.

Không đổi contract P43 đã chạy nếu regression xanh; chỉ sửa gap.

## 3. Full Ops Export Types

### P43 types

`INBOUND_ORDERS`, `SHIPMENTS`, `STOCKTAKES`, `RMA`.

### P44 types

`LOTS`, `EXCEPTIONS`, `LPNS`, `INVENTORY_BALANCES`, `WAVES`, `PUTAWAY_PROPOSALS`, `CROSS_DOCK_CANDIDATES`, `REPLENISHMENT_TASKS`.

## 4. Column Contracts

| Type | Columns tối thiểu, thứ tự ổn định |
|---|---|
| INBOUND_ORDERS | orderNo,status,supplierCode,expectedDate,warehouseCode,createdAt |
| SHIPMENTS | shipmentNo,status,customerCode,warehouseCode,plannedShipDate,createdAt |
| STOCKTAKES | stocktakeNo,status,warehouseCode,scheduledAt,completedAt |
| RMA | rmaNo,status,customerCode,reasonCode,createdAt |
| LOTS | lotNo,sku,productName,expiryDate,status,quantity,warehouseCode |
| EXCEPTIONS | exceptionNo,type,severity,status,entityType,entityRef,createdAt |
| LPNS | lpnCode,status,locationCode,warehouseCode,itemCount,updatedAt |
| INVENTORY_BALANCES | warehouseCode,locationCode,sku,lotNo,onHand,allocated,available,uomCode |
| WAVES | waveNo,status,priority,orderCount,lineCount,createdAt,releasedAt |
| PUTAWAY_PROPOSALS | proposalNo,status,lpnCode,fromLocation,toLocation,sku,quantity,createdAt |
| CROSS_DOCK_CANDIDATES | candidateNo,status,inboundRef,outboundRef,sku,quantity,createdAt |
| REPLENISHMENT_TASKS | taskNo,status,priority,sku,fromLocation,toLocation,quantity,createdAt |

EP0 phải map tên field thật. Không bịa dữ liệu; cột không có nguồn thật phải được sửa contract trước implementation.

## 5. Export Rules

- `ops.export` backend-enforced.
- Tenant filter mọi query.
- Cap 5.000; metadata báo truncated nếu đạt cap.
- `AsNoTracking` + projection; không N+1.
- CSV UTF-8 BOM, RFC4180 escaping.
- Neutralize formula prefix `=`, `+`, `-`, `@`, tab, CR.
- XLSX typed cells; không công thức từ user data.
- Date/time UTC/ISO contract thống nhất.
- Filename sanitize + type + timestamp.
- Invalid type: 400 `OPS_EXPORT_TYPE_INVALID`.

## 6. Frontend

- Shared `OpsExportButtons` trên đủ 12 list/detail contexts đã khóa.
- CSV/XLSX action theo permission.
- Loading/disable double click/error toast.
- Download blob, revoke URL.
- VI/EN parity.

## 7. Permission Matrix

| Scope | Permission |
|---|---|
| Master preview/commit | `master_data.import` |
| Master template/export | `master_data.export` |
| Ops CSV/XLSX | `ops.export` |

Seed/check permission regression; không tạo permission trùng.

## 8. Tests

- 4 Master types × CSV/XLSX × preview/commit/export/roundtrip.
- Duplicate/natural-key/foreign-key validation.
- 12 Ops types × CSV/XLSX.
- Empty dataset header-only.
- 5.001 rows cap/truncation.
- Unicode/comma/quote/newline/formula payload.
- Cross-tenant data absence.
- 403 từng permission.
- FE 12 contexts không duplicate request.

## 9. Definition of Done

- [ ] 4/4 Master IE types regression pass đủ CSV/XLSX.
- [ ] 12/12 Ops types pass đủ CSV/XLSX.
- [ ] Column order/contracts snapshot pass.
- [ ] Formula injection/tenant/permission gates pass.
- [ ] 12 frontend export contexts pass.
- [ ] `tests/verify_spreadsheet_exports_p46c.ps1` pass.

## 10. Rollback

Export handlers/type registry và FE buttons có thể revert từng type. Không rollback dữ liệu Master đã commit; dùng audit/compensating action theo policy hiện có.
