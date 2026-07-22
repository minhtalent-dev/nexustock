# Thẩm định Nexustock vs 3 dự án kho tham chiếu

**Ngày:** 2026-07-22  
**Phạm vi:** Đối chiếu khả năng / kiến trúc / độ phủ nghiệp vụ giữa **Nexustock** và 3 dự án đã add vào workspace dùng làm tham chiếu khi thiết kế sản phẩm.  
**Verdict tổng:** **PHÙ HỢP** — Nexustock là **siêu tập (superset) hiện đại hóa** của các năng lực cốt lõi từ 3 tham chiếu, đủ đóng roadmap product; một số flow nhà máy GCM rất đặc thù chưa port 1:1 (xem gap).

---

## 1. Mục đích

Trả lời câu hỏi:

> Sau khi hoàn tất full plan Nexustock (Phase 01–33), hệ thống có **phù hợp** với bài toán kho đã được chứng minh thực tế qua 3 dự án tham chiếu hay chưa?

Tiêu chí thẩm định:

| # | Tiêu chí |
|---|---|
| T1 | Bao phủ nghiệp vụ cốt lõi của từng tham chiếu |
| T2 | Kiến trúc / nền tảng có vượt mặt hạn chế của tham chiếu |
| T3 | Có lỗ hổng chặn thay thế vận hành thực tế không |
| T4 | Ranh giới: cái gì **không** cần copy 1:1 |

---

## 2. Hồ sơ 3 dự án tham chiếu

### 2.1 `1_GCM_Part` — GCM SMV PART

| Hạng mục | Thực tế disk |
|---|---|
| Đường dẫn | `D:\1_Project\2_GCM\1_GCM_Part` |
| Vai trò | Kho **phụ liệu** nhà máy (lot phụ liệu, IQC, hold, set thiết bị) |
| Stack | VB.NET WinForms · .NET Framework 4.8 · SQL Server |
| UI | Desktop thick-client (~**108** form `frm*.vb`) |
| Thiết bị | Handy scanner Bluetooth BT-1500, QR/in nhãn, Excel report |
| Đa ngôn ngữ | en / ja / zh (+ biến thể) |

**Năng lực nghiệp vụ chính (từ README + form map):**

- Tạo / in lại lot phụ liệu  
- Nhập / xuất / xuất nhỏ (kowake) / di chuyển vị trí  
- IQC input/list/output + QC input  
- Hold / unhold nguyên liệu  
- VMI accept, invoice divide, tana (kiểm kê so sánh)  
- Return / discard / extend validity / rework lot  
- Tích hợp handy + in tem  

**Bản chất:** ứng dụng **shopfloor chuyên sâu 1 nhà máy / 1 domain phụ liệu**, gắn chặt quy trình Sharp/GCM.

---

### 2.2 `2_GCM_Shipping` — GCM Shipping (FPRD)

| Hạng mục | Thực tế disk |
|---|---|
| Đường dẫn | `D:\1_Project\2_GCM\2_GCM_Shipping` |
| Vai trò | **Đóng gói + FIFO + đăng ký vận chuyển** xuất khẩu |
| Stack | VB.NET WinForms · .NET Framework 4.8 · SQL Server |
| UI | Desktop (~**47** form trong `GCM_PART`) |
| Thiết bị | Keyence Handy BT-1500 (CSV/COM), máy in nhãn |
| Điểm nổi bật | Non-admin auto-update, FIFO check, shipment registration |

**Năng lực nghiệp vụ chính:**

- Warehouse-in / cancel  
- Organize package set + FIFO check  
- Organize shipment set + FIFO check  
- Export approval / shipment registration / destination  
- Weight input, invoice input, reprint label  
- Handy sync + print packing/label  

**Bản chất:** ứng dụng **outbound shipping / packing station** chuyên sâu, không phải WMS full-cycle.

---

### 2.3 `warehouse-main` — Laravel WMS web

| Hạng mục | Thực tế disk |
|---|---|
| Đường dẫn | `D:\1_Project\warehouse-main\warehouse-main` |
| Vai trò | WMS **web admin** hiện đại (catalog + stock ops) |
| Stack | Laravel 13 · Filament 5 · PostgreSQL · Redis · Docker |
| UI | Admin panel Filament (không RF/mobile riêng như Nexustock) |
| Models chính | Product, Warehouse, StorageLocation, PO, StockImport/Export/Transfer/Adjustment, Serial*, RBAC, WorkflowApproval |

**Năng lực nghiệp vụ chính:**

- Master: product, brand, category, unit, supplier, warehouse, location  
- Stock: import / export / transfer / adjustment / movement / alert  
- Serial tracking (import/movement/transfer)  
- PO, team–warehouse assignment, permission/role, audit/activity  

**Bản chất:** **WMS-lite web** tốt cho back-office; thiếu sóng wave/allocation/putaway rule/local-agent/cutover mức Nexustock.

---

## 3. Hồ sơ Nexustock (đối tượng thẩm định)

| Hạng mục | Thực tế disk |
|---|---|
| Đường dẫn | `D:\1_Project\48_Nexustock` |
| Vai trò | **WMS Modular Monolith** full product |
| Stack | .NET 8 Web API · EF Core · PostgreSQL · Redis · Next.js 16 · Local Agent Windows |
| Roadmap | Phase **01–33** (+31a) — **34/34 ✅** (2026-07-22) |
| Modules backend | 26+ module (Identity → Readiness) |
| UI | Admin Next.js (**59** pages) + Mobile RF (**7**) + Health UI |
| i18n | VI/EN product-wide (Milestone 5) |

**Siêu năng lực so với cả 3 tham chiếu:** Rule engine, Putaway, Allocation/Reservation, Wave + Put-Wall, Cross-dock, Genealogy, Local Agent (scale/print), Webhook/Outbox, Labor KPI, Task interleaving, Readiness/Cutover freeze, Feature flags, Observability.

---

## 4. Ma trận phủ nghiệp vụ (T1)

Ký hiệu: ✅ đủ tương đương hoặc mạnh hơn · ◐ một phần / khác hình thái · ❌ thiếu có chủ đích hoặc chưa port · N/A không thuộc domain tham chiếu.

### 4.1 vs GCM Part (kho phụ liệu)

| Năng lực tham chiếu | Nexustock | Ghi chú |
|---|:---:|---|
| Lot create / reprint | ✅ | Lot + label printing module |
| Inbound nhận hàng | ✅ | Inbound + PO/ERP contract |
| Xuất / cấp liệu | ✅ | Outbound picking/packing |
| Di chuyển vị trí | ✅ | Inventory movement + mobile movement |
| QC / Hold-Release | ✅ | Qc module + hold semantics |
| IQC form nhà máy chi tiết | ◐ | Có QC generic; form IQC Sharp-specific không clone |
| VMI / invoice divide / CAP organize | ◐/❌ | Không port 1:1 flow Sharp; có thể mở rộng Rules/Inbound |
| Handy Bluetooth thick-client | ◐ | Mobile web + Local Agent; khác BT1500 desktop DLL |
| Kiểm kê (tana) | ✅ | Cycle count / stocktake admin |
| Return / scrap / RMA | ✅ | Rma + exception framework |
| Đa ngôn ngữ ja/zh | ◐ | Product VI/EN; ja/zh là mở rộng catalogs |

### 4.2 vs GCM Shipping (đóng gói / vận chuyển)

| Năng lực tham chiếu | Nexustock | Ghi chú |
|---|:---:|---|
| Warehouse-in thành phẩm | ✅ | Inbound / inventory |
| Package organize | ✅ | Outbound packing |
| FIFO / FEFO check | ✅ | Allocation FEFO/FIFO + rules |
| Shipment registration | ✅ | Outbound shipment flow |
| Export approval workflow | ◐ | Có RBAC + exceptions; không clone form approval FPRD |
| Weight / scale input | ✅ | Scale integration + Local Agent |
| Label reprint + audit | ✅ | Label printing + reprint reason |
| Handy terminal station | ◐ | Mobile RF + agent; khác Keyence CSV COM desktop |
| Non-admin auto-update desktop | N/A | Web/deploy Docker — mô hình khác |

### 4.3 vs warehouse-main (Laravel WMS)

| Năng lực tham chiếu | Nexustock | Ghi chú |
|---|:---:|---|
| Product / UoM / warehouse / location | ✅ | MasterData |
| Supplier / partner | ✅ | MasterData partners |
| PO + stock import/export/transfer | ✅ | Inbound/Outbound/Inventory |
| Stock adjustment / alert | ✅ | Cycle count + observability alerts |
| Serial | ✅ | Serial module + mobile receive |
| RBAC / audit | ✅ | Identity + audit + observability |
| Filament admin UX patterns | ◐ | Next.js admin khác stack; đủ CRUD/ops |
| Team–warehouse assignment | ◐ | Tenant + permission; model team khác |
| Wave / putaway / allocation / RF / agent | ✅ **vượt** | warehouse-main không có / mỏng |

---

## 5. So sánh kiến trúc (T2)

| Trục | GCM Part / Shipping | warehouse-main | Nexustock |
|---|---|---|---|
| Client | WinForms desktop | Filament web | Next.js web + mobile web |
| Backend | Form + SQL trực tiếp | Laravel monolith | Modular Monolith API |
| DB | SQL Server | PostgreSQL | PostgreSQL |
| Thiết bị | DLL/COM Handy + printer | Hạn chế | Local Agent WSS + mobile scan |
| Deploy | Publish folder / auto-update EXE | Docker | Docker + scripts backup/rollback |
| Tách module | Thấp (form-centric) | Model/Filament resources | Module boundary rõ (26+) |
| Integration | Nội bộ nhà máy | Chủ yếu nội bộ | ERP contract + Webhook + Idempotency |
| Go-live gate | Thủ công | Middleware Docker | Readiness/Cutover freeze |

**Kết luận T2:** Nexustock **đúng hướng hiện đại hóa** so với GCM (thoát thick-client/SQL Server gắn máy) và **mạnh hơn** warehouse-main về WMS nâng cao + RF + thiết bị + vận hành production.

---

## 6. Gap & rủi ro (T3)

### 6.1 Gap không chặn product DoD (chấp nhận được)

| Gap | Mức | Lý do chấp nhận |
|---|---|---|
| Flow Sharp-specific (VMI panel, invoice divide, CAP, Ford code, wafer lot…) | Trung bình | Domain nhà máy; Nexustock là product generic — mở rộng bằng Rules/custom module khi cần |
| Handy BT-1500 desktop COM/CSV y hệt GCM | Trung bình | Đã có Mobile RF + Local Agent; adapter Keyence có thể phase sau |
| ja / zh / pt localization | Thấp | Catalog architecture sẵn; chỉ thiếu locale packs |
| Desktop non-admin auto-update | Thấp | Mô hình web không cần |

### 6.2 Điểm Nexustock vượt hẳn tham chiếu (giá trị thay thế)

- Allocation / Reservation / Putaway slotting / Wave Put-Wall  
- Cross-dock, Genealogy cascade hold  
- Task interleaving + Labor KPI  
- Webhook Outbox/DLQ, Feature flags, Observability  
- Readiness Gate + cutover freeze  
- i18n product **59/59** VI/EN  

### 6.3 Rủi ro vận hành nếu “thay thế cứng” GCM ngay

1. Operator quen WinForms + handy CSV — cần UAT chuyển đổi UX.  
2. Báo cáo/Excel template nhà máy — cần mapping lại.  
3. Kết nối DB/IP nội bộ GCM — không mang sang; Nexustock dùng PostgreSQL/API.  

→ Phù hợp **product mới / migration có giai**, không phải drop-in binary của GCM.

---

## 7. Kết luận thẩm định (T4)

### Verdict

| Câu hỏi | Trả lời |
|---|---|
| Có phù hợp với 3 tham chiếu để tạo nên Nexustock? | **Có — PHÙ HỢP** |
| Nexustock có “đủ” so với từng tham chiếu? | **Đủ ở lớp WMS cốt lõi**; **mạnh hơn** ở lớp advanced; **không clone 100%** flow Sharp |
| Full plan 01–33 có đóng đúng sứ mệnh tham chiếu không? | **Có** — hợp nhất Part + Shipping + Web WMS thành một nền tảng |

### Điểm số định tính (thang 10)

| Trục | Điểm | Ghi chú |
|---|:---:|---|
| Phủ Part (kho phụ liệu) | **8.5** | Thiếu form IQC/VMI đặc thù |
| Phủ Shipping (pack/ship) | **8.5** | FIFO/pack/ship/scale/label đủ |
| Phủ warehouse-main | **9.5** | Siêu tập rõ |
| Kiến trúc hiện đại / mở rộng | **9.5** | Modular + agent + readiness |
| **Tổng hợp phù hợp product** | **9.0 / 10** | Đủ tuyên bố “đóng roadmap tham chiếu” |

---

## 8. Khuyến nghị tiếp theo

1. **Giữ Nexustock là SoT product** — không quay lại WinForms GCM cho product mới.  
2. Thay GCM Part (IQC): execute **Phase 34** — [`phase_34_iqc_ux_map_gcm.md`](phases/phase_34_iqc_ux_map_gcm.md) + [`IQC_UX_MAP_GCM_PART.md`](IQC_UX_MAP_GCM_PART.md).  
3. Adapter Handy Keyence (nếu cần desktop parity) — wave riêng sau P34.  
4. Locale `ja`/`zh` — mở rộng catalogs khi cần.  
5. Ops: gói ký go-live production (P30 ngoài DoD code).

---

## 9. Nguồn đối chiếu

| Nguồn | Path |
|---|---|
| GCM Part | `D:\1_Project\2_GCM\1_GCM_Part\README.md` + `frm*.vb` |
| GCM Shipping | `D:\1_Project\2_GCM\2_GCM_Shipping\README.md` + `GCM_PART\frm*.vb` |
| warehouse-main | `D:\1_Project\warehouse-main\warehouse-main\README.md` + `src\app\Models\*` |
| Nexustock | `D:\1_Project\48_Nexustock\README.md` + `planning\IMPLEMENTATION_PLAN.md` + `backend\modules\*` |

---

**Chữ ký thẩm định:** JARVIS · 2026-07-22 · Status: **APPROVED — phù hợp tham chiếu / product closed**
