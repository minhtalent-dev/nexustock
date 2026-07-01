# Sample data pack (Dữ liệu mẫu phục vụ UAT và Integration Testing)

Tài liệu cung cấp danh mục dữ liệu mẫu chuẩn để seed vào cơ sở dữ liệu phục vụ phát triển và kiểm thử.

---

## 1. Dữ liệu tổ chức (Organization & Warehouse Layout)

### 1.1 Tenant & Warehouse

| tenantId | Tên Tenant | warehouseId | Tên Kho | Địa chỉ |
|---|---|---|---|---|
| `tenant_nexustock_demo` | Công ty Cổ phần sữa Việt Demo | `wh_hn_01` | Kho Tổng Hà Nội | Số 1 Đại Cồ Việt, Hai Bà Trưng, HN |
| `tenant_nexustock_demo` | Công ty Cổ phần sữa Việt Demo | `wh_hcm_01` | Kho Chi Nhánh HCM | Cát Lái, Quận 2, TP. Hồ Chí Minh |

### 1.2 Zones (Khu vực trong kho `wh_hn_01`)

| zoneId | zoneCode | Tên Khu vực | zoneType | Ghi chú |
|---|---|---|---|---|
| `zone_receiving_01` | `REC_ZONE` | Khu vực nhận hàng | staging | Nơi dỡ hàng từ container |
| `zone_cool_01` | `COOL_ZONE` | Khu lạnh (2-8°C) | storage | Lưu trữ sữa tươi, vắc xin |
| `zone_dry_01` | `DRY_ZONE` | Khu khô thường | storage | Lưu sữa bột, kệ cao |
| `zone_qc_01` | `QC_ZONE` | Khu kiểm soát chất lượng | quarantine | Nơi cô lập hàng nghi ngờ lỗi |
| `zone_shipping_01` | `SHIP_ZONE` | Khu chuẩn bị xuất hàng | shipping | Nơi tập kết hàng chuẩn bị lên xe |

### 1.3 Locations (Vị trí chi tiết trong kho `wh_hn_01`)

| locationId | locationCode | zoneId | locationType | capacity (pallet) | status |
|---|---|---|---|---|---|
| `loc_rec_01` | `REC-01` | `zone_receiving_01` | staging | 10 | active |
| `loc_dry_a01` | `DRY-A-01` | `zone_dry_01` | storage | 2 | active |
| `loc_dry_a02` | `DRY-A-02` | `zone_dry_01` | storage | 2 | active (Locked) |
| `loc_cool_b01` | `COOL-B-01` | `zone_cool_01` | storage | 1 | active |
| `loc_qc_hold_01`| `QC-HOLD-01` | `zone_qc_01` | quarantine | 5 | active |
| `loc_ship_01` | `SHIP-01` | `zone_shipping_01` | shipping | 10 | active |

---

## 2. Danh mục Hàng hóa (Items, UOMs & Packages)

### 2.1 Items (Vật tư)

| itemId | itemCode | Tên mặt hàng | trackingPolicy | shelfLifeDays | status |
|---|---|---|---|---|---|
| `item_milk_dry` | `MILK-DRY-900` | Sữa bột Optimum 900g | Lot | 730 | active |
| `item_milk_fresh`| `MILK-FRSH-180`| Sữa tươi tiệt trùng 180ml | Lot_Expiry | 180 | active |
| `item_scale_test`| `SCALE-TEST-KG`| Thùng carton cân thử nghiệm| Normal | 0 | active |
| `item_serial_ecu` | `ECU-HONDA-12` | Cụm điều khiển động cơ ECU | Serial | 1095 | active |

### 2.2 UOMs (Đơn vị tính)

| uomId | uomCode | Tên UOM | baseUomId | conversionFactor | status |
|---|---|---|---|---|---|
| `uom_lon` | `LON` | Lon | null | 1.000000 | active |
| `uom_hop` | `HOP` | Hộp | null | 1.000000 | active |
| `uom_thung` | `THUNG` | Thùng | null | 1.000000 | active |

### 2.3 Packages (Quy cách đóng gói)

| packageId | itemId | uomId | Tên quy cách | conversionFactor | barcode | status |
|---|---|---|---|---|---|---|
| `pkg_milk_dry_lon` | `item_milk_dry` | `uom_lon` | Lon 900g | 1.000000 | `8934673123456` | active |
| `pkg_milk_dry_thg` | `item_milk_dry` | `uom_thung` | Thùng 12 Lon | 12.000000 | `8934673123463` | active |
| `pkg_milk_frsh_hop`| `item_milk_fresh`| `uom_hop` | Hộp 180ml | 1.000000 | `8934673223453` | active |
| `pkg_milk_frsh_thg`| `item_milk_fresh`| `uom_thung` | Thùng 48 Hộp | 48.000000 | `8934673223460` | active |

---

## 3. Dữ liệu đơn hàng mẫu (Orders & Transactions)

### 3.1 InboundOrder (Phiếu nhập khẩu)

- **ID:** `inb_ord_001`
- **orderNo:** `PO-20260701-001`
- **partnerId:** `partner_vinamilk_01` (Nhà cung cấp sữa)
- **warehouseId:** `wh_hn_01`
- **status:** `open`
- **Items:**
  - Item: `item_milk_dry` | Expected Qty: `240` Lon (tương ứng 20 Thùng) | UOM: `LON` | tolerancePct: `5.00`
  - Item: `item_milk_fresh` | Expected Qty: `480` Hộp (tương ứng 10 Thùng) | UOM: `HOP` | tolerancePct: `0.00`

### 3.2 OutboundOrder (Phiếu xuất kho)

- **ID:** `out_ord_001`
- **shipmentNo:** `SO-20260701-001`
- **partnerId:** `partner_coopmart_01` (Khách hàng Siêu thị Co.opmart)
- **warehouseId:** `wh_hn_01`
- **priority:** `2` (Bình thường)
- **status:** `open`
- **Lines:**
  - Item: `item_milk_dry` | Requested Qty: `60` Lon | UOM: `LON`
  - Item: `item_milk_fresh` | Requested Qty: `96` Hộp | UOM: `HOP`

---

## 4. Dữ liệu tồn kho mẫu (Inventory Balances)

Để phục vụ kiểm thử phân bổ, nạp sẵn dữ liệu tồn kho sau tại kho `wh_hn_01`:

| id | locationId | itemId | lotNo | qty | inventoryStatus | lpnId |
|---|---|---|---|---|---|---|
| `bal_dry_01` | `loc_dry_a01` | `item_milk_dry` | `LOT-26A-01` | 120.000000 | `released` | `LPN-DRY-01` |
| `bal_dry_02` | `loc_dry_a01` | `item_milk_dry` | `LOT-26A-02` | 60.000000 | `released` | `LPN-DRY-02` |
| `bal_qc_01` | `loc_qc_hold_01` | `item_milk_dry` | `LOT-26A-03` | 100.000000 | `hold` | null |
| `bal_cool_01`| `loc_cool_b01` | `item_milk_fresh`| `LOT-26F-01` | 240.000000 | `released` | null |
