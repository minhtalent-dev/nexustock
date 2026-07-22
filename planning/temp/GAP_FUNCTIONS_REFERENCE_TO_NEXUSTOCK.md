# `rf` — Inventory chức năng tham chiếu còn thiếu trên Nexustock

**Ngày:** 2026-07-22  
**Workflow:** `rf` (reindex function) — quét form/model tham chiếu trên disk → đối chiếu code Nexustock  
**Câu hỏi:** Còn chức năng nào ở 3 project tham chiếu **chưa có / chưa đủ** khi code sang Nexustock?

**Nguồn disk:**
- Part: `D:\1_Project\2_GCM\1_GCM_Part` — **~55** form nghiệp vụ `frm*` (không Designer)
- Shipping: `D:\1_Project\2_GCM\2_GCM_Shipping\GCM_PART` — **~20** form FPRD
- warehouse-main: `src/app/Models` + Filament Resources
- Nexustock: Phase **01–35** ✅ + modules backend

**Đọc kèm:**  
[`ACCEPTANCE_FUNCTION_PARITY_REFERENCE_WAREHOUSES.md`](ACCEPTANCE_FUNCTION_PARITY_REFERENCE_WAREHOUSES.md) · [`AUDIT_FEATURES_MISFIT_REFERENCE_WAREHOUSES.md`](AUDIT_FEATURES_MISFIT_REFERENCE_WAREHOUSES.md)

---

## 1. Verdict ngắn

| Câu hỏi | Trả lời |
|---|---|
| Thiếu chức năng **core WMS** chặn product? | **Không** |
| Còn chức năng tham chiếu **chưa port**? | **Có** — chủ yếu **M1** (cấm clone) + **M3** (optional cutover site) |
| Cần phase mới bắt buộc ngay? | **Không** — trừ FOUNDER chốt site Sharp/Shipping cứng |

**Phân loại thiếu:**

| Mã | Nghĩa | # ước lượng |
|---|---|---:|
| **COVERED** | Đã có tương đương / mạnh hơn trên Nexustock | Đa số core |
| **PARTIAL** | Có shape khác / thiếu nhánh UX | ~8–12 |
| **MISS_M3** | Thiếu · nên cân nhắc nếu cutover site | ~8 |
| **MISS_M1** | Thiếu · **không** đưa vào core product | ~15+ |
| **N/A_STACK** | Chỉ thuộc desktop/EXE/Filament stack | ~5 |

---

## 2. GCM Part — từng form → Nexustock

### 2.1 COVERED (đã có logic đủ)

| Form tham chiếu | Chức năng | Nexustock |
|---|---|---|
| `frm101_PartLotCreate` | Tạo lot | Inbound receive + Lot |
| `frm102_PartLotRePrint` | In lại tem lot | Label printing + reprint |
| `frm104_Part_Input` / `frm106_LotPartAccept` | Nhận / accept lot | Inbound |
| `frm107_Part_Output` | Xuất | Outbound pick/ship |
| `frm108a_Part_Move_FC` | Di chuyển | Inventory move + mobile + **QcGate** |
| `frm108_Part_Return` | Trả | RMA / return |
| `frm113` / `frm114*` | IQC / QC input | `/admin/qc` result (P34) |
| `frm135_IqcOutput` | Xuất sau IQC | Outbound/move **sau Release** + Gate |
| `frm136_IqcList` | Danh sách IQC | QC queue filter/aging |
| `frm137_IqcInputResult` | Kết quả IQC | QC History tab |
| `smv_frm6_PartHold` (special) | Hold | QC Hold/Release |
| `frm139_PartLotList` / `frm198_LotInfomation` | Tra cứu lot | `/admin/lots` |
| `frm147` / `frm148` Tana | Kiểm kê upload/so sánh | Cycle count / stocktake |
| `frm125_LotQuantityModify` | Sửa SL | Inventory adjust (có kiểm soát) |
| `frmLogin` | Đăng nhập | Identity JWT |

### 2.2 PARTIAL — có trên Nexustock nhưng khác hình / thiếu UX

| Form | Gap còn lại | Mức | Hướng |
|---|---|---|---|
| `frm107a_Part_OutputKowake` | Xuất nhỏ / tách inner | M3 thấp | Pick partial + config mode |
| `frm107b_Part_OutputInnerOnly` | Chỉ xuất inner | M3 thấp | Outbound rule/mode |
| `frm103_ResinRePrint` | Tem resin đặc thù | M3 thấp | Label template, không form riêng |
| `frm112_Part_Set` / `frm124_LotPartWaitSet` | Set thiết bị / wait set | M3 thấp | LPN/kit / wave tùy site |
| `frm109_Part_Finish` | Finish lot shopfloor | PARTIAL | Status lot + outbound complete |
| `frm110` / `frm111` NormalTemp move/accept | Biến thể move/accept | PARTIAL | Gộp vào move/inbound |
| `frm149_ResultSearch` | Search kết quả rộng | PARTIAL | QC history + observability search |
| `frmIQCRequestReport` | Report Excel IQC | M3 TB | Export/report wave — không clone macro |
| `frm105a` / `frm113a` / `frm151a` InvoiceList | List hóa đơn nhập | PARTIAL | Inbound/ERP messages UI |
| `frm152_CItem_Input` | Nhập mã C-item | PARTIAL | Master attribute / custom field |

### 2.3 MISS_M3 — thiếu · cân nhắc khi cutover Parts site

| Form | Chức năng thiếu trên Nexustock | Khi nào làm |
|---|---|---|
| `frm115_LotDiscard` | Wizard scrap/discard lot chuyên biệt | Nếu UAT yêu cầu UI riêng (RMA/adjust đã cover một phần) |
| `frm116_LotValidExtendTime` | Gia hạn hiệu lực lot | Nếu lot shelf-life bắt buộc wizard |
| `frm117` / `frm118` ReturnLot Make/Return | Luồng trả lot nội bộ nhà máy | Nếu khác RMA customer |
| `frm119_LotDisable` | Vô hiệu hóa lot | QC Reject + block Gate ≈; UI “disable” riêng optional |
| `frm196_InpReworkLot` | Rework lot | Optional inventory/QC rework flow |
| Handy BT-1500 desktop | Adapter COM/CSV | Khi sàn bắt buộc máy cũ |

### 2.4 MISS_M1 — thiếu · **không** port vào core

| Form | Lý do |
|---|---|
| `frm126_VMI_Accept` / `frm151_BtVmiAccept` | VMI Sharp/site |
| `frm138*` InvoiceDivide | Chia hóa đơn nội bộ |
| `frm127_PartFormationFPC` | FPC nhà máy |
| `frm128_CAP_Organize` | CAP Sharp |
| `frm197_Enter_CTL_CD` | Mã CTL OEM |
| `frm129_Torikeshi` | Hủy nghiệp vụ gắn quy trình nội bộ (đánh giá case-by-case; cancel inbound/outbound đã có một phần) |
| `frm199_FactoryList` | Danh mục nhà máy EXE | Master/tenant config |
| `frmUpdateProgress` | Auto-update EXE | N/A web |

---

## 3. GCM Shipping — từng form → Nexustock

### 3.1 COVERED

| Form | Nexustock |
|---|---|
| `frm101_Fprd_Warehouse_In` (+ Cancel) | Inbound / cancel nhận |
| `frm103_Fprd_OrganizePackageSet` (+ FIFO_Check / Cancel / Select) | Outbound packing + Allocation FIFO/FEFO |
| `frm104_Fprd_OrganizeShipmentSet` (+ FIFO_Check / Cancel / Select) | Outbound shipment + allocation |
| `frm105_Fprd_Reprint` | Label reprint audit |
| `frm107_Fprd_ShipmentRegistration` (+ Cancel) | Outbound ship register |
| `frm109_Fprd_VariousWeightInput` | Scale + packing weight (P21) |
| `frmLogin*` | Identity |

### 3.2 PARTIAL / MISS_M3

| Form | Gap | Mức | Hướng |
|---|---|---|---|
| `frm106_Fprd_ExportApproval` (+ Cancel) | Phê duyệt xuất khẩu multi-step | **M3 TB** | Approval mỏng trên Outbound (không clone) |
| `frm108_Fprd_DestinationRegistration` | Master đích ship cứng EXE | M3 thấp | Partners / locations master |
| `frm110_Fprd_InvoiceInput` | Hóa đơn vận chuyển | M3 thấp | ERP docs / outbound attachment |
| `frm102_Fprd_InternalProductModify` | Sửa SP nội bộ packing | PARTIAL | MasterData edit + outbound line amend |
| Keyence handy CSV/COM | Adapter máy cũ | M3 TB | Device wave |
| `frmUpdateProgress` | Auto-update EXE | N/A_STACK | Docker/web |

### 3.3 MISS_M1

| Form | Lý do |
|---|---|
| `frm03e_Part_Wafer_LotSeparation` | Semiconductor wafer — domain khác |

---

## 4. warehouse-main — model/resource → Nexustock

### 4.1 COVERED

| Model / Resource | Nexustock |
|---|---|
| Product, Unit, Warehouse, StorageLocation | MasterData |
| Supplier, Contact | Partners |
| PurchaseOrder (+ Item) | Inbound + ERP PO |
| StockImport / Export / Transfer / Movement / Adjustment | Inbound / Outbound / Inventory / Stocktake |
| Inventory, InventoryLocation | Inventory balances |
| Serial* (Number, Import, Movement, Transfer, ProductSerial) | Serial module |
| StockAlert | Observability alerts |
| User, Role, Permission | Identity |
| CustomActivitylog | Audit / timeline |
| Setting | Config + Feature flags |

### 4.2 PARTIAL / MISS_M3 (optional)

| Model | Gap Nexustock | Mức |
|---|---|---|
| **Brand** | Không có entity Brand riêng | M3 thấp — attribute/product field |
| **Category** | Không category tree Filament | M3 thấp |
| **ProductCondition** | Condition catalog | M3 thấp — reason/QC status cover một phần |
| **Team** / **TeamWarehouseAssignment** | Không team–WH matrix | M3 thấp — Tenant+Role đủ product |
| **WorkflowApproval** | Không multi-step approval entity | M3 TB — nếu compliance |
| **Product images gallery** | Không gallery | M3 thấp nice-to-have |
| Inventory sync manager page | Khác shape ERP sync | PARTIAL — ErpIntegration + webhook |

### 4.3 MISS_M1 / N/A

| Model | Lý do |
|---|---|
| **Post** | CMS — không thuộc WMS |
| Filament-only UX patterns | Stack khác — Next.js đã cover ops |

---

## 5. Bảng tổng — “còn thiếu gì đáng quan tâm?”

### 5.1 Không thiếu (core đã đủ)

Inbound · Lot · IQC/QC/Hold/Gate · Move · Pick/Pack/Ship · FIFO/FEFO allocation · Stocktake · Serial · Label/Scale/Agent · RMA · Wave · Labor · TI · i18n VI/EN · Ops nav · Readiness  

### 5.2 Thiếu **có thể làm** (M3) — backlog cutover, không blocker product

| # | Thiếu | Nguồn | Ưu tiên |
|---|---|---|---|
| 1 | Export approval multi-step | Shipping frm106 | P1 nếu Shipping cutover |
| 2 | Handy Keyence/BT adapter | Part+Shipping | P2 nếu máy cũ bắt buộc |
| 3 | Lot extend / discard / rework wizards | Part frm115–119, 196 | P2 nếu UAT Parts |
| 4 | Kowake / inner-only UX mode | Part frm107a/b | P2 |
| 5 | Destination/invoice ship docs | Shipping frm108/110 | P2 via ERP |
| 6 | Brand / Category / Team–WH | warehouse-main | P3 |
| 7 | WorkflowApproval generic | warehouse-main | P3 compliance |
| 8 | ja/zh locale packs | GCM | P3 thị trường |
| 9 | Excel plant report 1:1 | Part/Shipping | P2 reporting |
| 10 | DF-01 offline MOVE reserved qty | (nội bộ Nexustock) | P2 patch nhỏ |

### 5.3 Thiếu **đúng khi thiếu** (M1 — không code vào core)

VMI · Invoice divide · CAP · FPC · CTL_CD · Wafer lot · Post/CMS · Clone WinForms/Filament · Dual product Part/Shipping  

---

## 6. Kết luận `rf`

1. **Quét form Part (~55) + Shipping (~20) + Models WH-main (~40):** không phát hiện thiếu **core** chưa được cover bởi roadmap 01–35.  
2. **Còn thiếu** = danh sách **§5.2 M3** (optional site) + **§5.3 M1** (cố ý không làm).  
3. Audit tổng / nghiệm thu trước **không mâu thuẫn** — `rf` này **làm đầy inventory form-by-form**.  
4. FOUNDER chỉ cần mở phase mới khi chốt **1–2 mục M3** (thường: Export approval hoặc Handy adapter).

### Khuyến nghị

| Nếu mục tiêu | Action |
|---|---|
| Product generic go-live | **Không** bắt buộc phase mới từ gap tham chiếu |
| Cutover Parts Sharp | Ưu tiên §5.2 #3–4 + training IQC (P34 đã có) |
| Cutover Shipping FPRD | Ưu tiên §5.2 #1 + #5 |
| Giữ máy Keyence cũ | §5.2 #2 |

---

## 7. Liên kết cập nhật

| Tài liệu | Vai trò |
|---|---|
| File này | **SoT inventory thiếu** sau `rf` |
| ACCEPTANCE … | Nghiệm thu đúng/đủ + deep §9 |
| AUDIT_FEATURES_MISFIT … | Phân loại M1–M4 |

---
JARVIS · `rf` gap inventory · 2026-07-22
