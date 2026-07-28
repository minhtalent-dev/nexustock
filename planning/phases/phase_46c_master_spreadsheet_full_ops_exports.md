# PHASE 46C: Master Spreadsheet Regression + Full Ops Exports

## Execution Spec

| Mục | Giá trị |
|---|---|
| Trạng thái | ✅ **Hoàn thành** — DoR/DoD 100%; strict automated verification PASS (76 integration tests + frontend tsc/eslint PASS) |
| Ước lượng | 2–3 dev-days |
| Upstream | P43; P46A; P46B **ĐÓNG** |
| Downstream | P46D; P46E |
| Scope nguồn | P43 Master IE 4 + Ops exports 4; P44 Ops exports 8 |
| Quyết định khóa | Header `X-Export-Truncated`, Cap 5.000 rows, shared `OpsExportButtons`, `ops.export` + `master_data.*` permissions |
| Execution plan | [implementation_plan.md](file:///C:/Users/mes/.gemini/antigravity-ide/brain/1267c81d-0c4e-4711-94e6-b361e1c233a4/implementation_plan.md) |

### Changelog plan

| Ngày | Thay đổi |
|---|---|
| 2026-07-24 | Khởi tạo child spec P46C thuộc umbrella Phase 46 |
| 2026-07-28 | `rp1`: Rà soát mã nguồn, phát hiện 8/12 Ops export types chưa có backend handler, 8/12 UI pages chưa mount `OpsExportButtons`, thiếu `[Authorize]` trên `ImportsController`; nâng đặc tả lên 100% Execution-Ready |
| 2026-07-28 | `rp2`: Reindex hàm/controller/UI, lập kế hoạch thực thi chi tiết chuẩn 100% qua `[17-auto-plan]` cho 12 Ops Exports và remediation Master IE |
| 2026-07-28 | `/18-auto-execute`: Hoàn thành Master Data RBAC, chống Formula Injection CSV/XLSX và 12 Ops Exports backend/frontend. |
| 2026-07-28 | `rp4` + `rp5`: Đóng P46C; strict verifier PASS, 76/76 integration tests, frontend typecheck/lint sạch. |

> [!IMPORTANT]
> Không còn câu hỏi mở. Child spec này là SoT phạm vi P46C; `implementation_plan.md` trong brain là SoT thứ tự triển khai file-level.

---

## 1. Mục tiêu

Khóa và xác thực 100% spreadsheet/export scope P43–P44:
1. **Master Data Import/Export (4 types):** UOMS, WAREHOUSES, ZONES, REASONS với preview, commit, error CSV, export CSV/XLSX và roundtrip verification.
2. **Ops Exports (12 types):** Đủ 12 danh mục vận hành với schema cột chuẩn hóa từ entity thật, chống N+1, giới hạn 5.000 dòng, bảo mật chống CSV formula injection, phân quyền backend và tenant isolation.

---

## 2. P43 Master Import/Export Regression & Remediation

### Remediation P0 (Security & Permission)

- **`ImportsController`**: Thêm `[Authorize]` và kiểm tra quyền `master_data.import` đối với Preview/Commit/Errors.
- **`ExportsController`**: Đảm bảo kiểm tra quyền `master_data.export` cho tất cả các loại Master data export (`ITEMS`, `LOCATIONS`, `PARTNERS`, `UOMS`, `WAREHOUSES`, `ZONES`, `REASONS`).
- **Batch Isolation**: Gắn `TenantId` bắt buộc trên `ImportBatch` và query predicate `x.TenantId == tenantId`.

### Master Data Canonical Contracts

| Type | Data Columns bắt buộc | Rules & Validation |
|---|---|---|
| `UOMS` | `code,name,isActive` | Code unique, max 20. Name max 100. |
| `WAREHOUSES` | `code,name,description,isActive` | Code unique, max 50. Name max 150. |
| `ZONES` | `warehouseCode,code,name,zoneType` | WhCode tồn tại. Zone Code unique per Wh. ZoneType enum. |
| `REASONS` | `code,reasonType,description,isActive` | Code unique. ReasonType enum. |

> **Ghi chú:** Đã loại bỏ cột `errorMessage` khỏi file clean Export. Cột `errorMessage` chỉ xuất hiện ở cột cuối của Template mẫu và File Error CSV.

---

## 3. Full Ops Export Types (12 Types)

### Nhóm P43 Baseline (4 types)
- `INBOUND_ORDERS`: Đơn nhập kho.
- `SHIPMENTS`: Đơn xuất kho.
- `STOCKTAKES`: Đợt kiểm kê.
- `RMA`: Yêu cầu hàng trả về.

### Nhóm P44 Extended (8 types)
- `LOTS`: Quản lý lô hàng & hạn dùng.
- `EXCEPTIONS`: Ngoại lệ vận hành.
- `LPNS`: Mã Pallet/Thùng LPN.
- `INVENTORY_BALANCES`: Tồn kho theo vị trí & lô.
- `WAVES`: Đợt lấy hàng (Wave picking).
- `PUTAWAY_PROPOSALS`: Đề xuất cất hàng.
- `CROSS_DOCK_CANDIDATES`: Ứng viên chuyển tiếp trực tiếp.
- `REPLENISHMENT_TASKS`: Nhiệm vụ bổ sung tồn kho.

---

## 4. Column Contracts (12 Ops Export Schema)

Các cột xuất dữ liệu được ánh xạ 1:1 từ thuộc tính Entity thực tế trong backend (không sử dụng cột giả lập không có trong DB):

| Type | Cột xuất dữ liệu (Thứ tự cố định) | Nguồn Entity/DbContext |
|---|---|---|
| `INBOUND_ORDERS` | `orderNo,status,partnerId,createdAt,createdBy` | `InboundDbContext.InboundOrders` |
| `SHIPMENTS` | `shipmentNo,status,partnerId,createdAt,createdBy` | `InventoryDbContext.Shipments` |
| `STOCKTAKES` | `stocktakeNo,status,totalVarianceAmount,createdAt,createdBy` | `InventoryDbContext.Stocktakes` |
| `RMA` | `rmaNo,status,customerId,referenceNo,createdAt,createdBy` | `RmaDbContext.RmaRequests` |
| `LOTS` | `lotNo,itemId,qcStatus,expiryDate,productionDate` | `InboundDbContext.Lots` |
| `EXCEPTIONS` | `code,type,severity,status,referenceType,referenceId,locationId,lotNo,qty,reasonCode,note,createdAt` | `ExceptionsDbContext.OperationalExceptions` |
| `LPNS` | `lpnNo,locationId,status,createdAt,createdBy,updatedAt,updatedBy` | `LpnDbContext.Lpns` |
| `INVENTORY_BALANCES` | `itemId,lotNo,locationId,qtyOnHand,qtyReserved,qtyAvailable,lpnId,createdAt,updatedAt` | `InventoryDbContext.Inventories` |
| `WAVES` | `waveNo,status,createdAt,createdBy,updatedAt` | `WaveDbContext.PickingWaves` |
| `PUTAWAY_PROPOSALS` | `warehouseId,lotId,itemId,qty,candidateLocationId,score,reason,status,createdAt` | `PutawayDbContext.PutawayProposals` |
| `CROSS_DOCK_CANDIDATES` | `lotId,inboundOrderItemId,waveItemId,itemId,qtyAvailable,qtyRequested,qtyMatched,matchScore,status,expiresAt,createdAt` | `CrossDockingDbContext.Candidates` |
| `REPLENISHMENT_TASKS` | `itemId,sourceLocationId,targetLocationId,lotNo,requestedQty,actualQty,status,mobileTaskId,createdAt` | `ReplenishmentDbContext.ReplenishmentTasks` |

---

## 5. Export Rules & Guardrails

1. **Phân quyền Backend:** `ops.export` được kiểm tra chặt chẽ trên endpoint `GET /api/ops/exports`. Người dùng thiếu quyền nhận HTTP `403 Forbidden`.
2. **Tenant Isolation:** Mọi query export bắt buộc lọc `TenantId` của User đăng nhập.
3. **Giới hạn dòng (Cap 5.000):**
   - Query sử dụng `.Take(5001)`.
   - Nếu kết quả > 5.000 dòng: Chỉ ghi 5.000 dòng đầu vào file xuất và thêm HTTP Response Header: `X-Export-Truncated: true`. Dưới hoặc bằng 5.000 dòng trả `X-Export-Truncated: false`.
   - Không trả lỗi HTTP 400 gây gián đoạn trải nghiệm người dùng.
4. **Hiệu năng Query:**
   - Sử dụng `.AsNoTracking()` và Projection `.Select(...)` server-side.
   - Sắp xếp cố định (`OrderBy` + Primary Key tie-breaker) đảm bảo tính nhất quán giữa các lần xuất.
5. **An toàn Mã độc & Công thức (CSV/XLSX Security):**
   - **CSV:** UTF-8 BOM (`EF BB BF`), chuẩn hóa RFC4180. Neutralize các ô bắt đầu bằng `=`, `+`, `-`, `@`, tab (`\t`), CR (`\r`) bằng cách chèn dấu nháy đơn `'`.
   - **XLSX:** Ghi kiểu dữ liệu ô rõ ràng (String, Numeric, DateTime), không sinh công thức tính toán từ dữ liệu người dùng.
6. **Đặt tên File:**
   - Format: `{fileBase}_{yyyyMMddHHmmss}.{ext}` (Ví dụ: `inventory_balances_20260728104500.xlsx`).

---

## 6. Frontend Wiring (12 UI Contexts)

Cập nhật component dùng chung `@/components/ops-export-buttons.tsx` hỗ trợ đủ 12 `OpsExportType` và mount tại 12 trang quản trị:

| Trang UI (`frontend/src/app/admin/`) | Export Type | Vị trí mount |
|---|---|---|
| `inbound/page.tsx` | `INBOUND_ORDERS` | Header Action Toolbar |
| `outbound/page.tsx` | `SHIPMENTS` | Header Action Toolbar |
| `inventory/stocktakes/page.tsx` | `STOCKTAKES` | Header Action Toolbar |
| `rma/page.tsx` | `RMA` | Header Action Toolbar |
| `lots/page.tsx` | `LOTS` | Header Action Toolbar |
| `exceptions/page.tsx` | `EXCEPTIONS` | Header Action Toolbar |
| `lpn/page.tsx` | `LPNS` | Header Action Toolbar |
| `inventory/page.tsx` | `INVENTORY_BALANCES` | Header Action Toolbar |
| `waves/page.tsx` | `WAVES` | Header Action Toolbar |
| `putaway/page.tsx` | `PUTAWAY_PROPOSALS` | Header Action Toolbar |
| `cross-docking/page.tsx` | `CROSS_DOCK_CANDIDATES` | Header Action Toolbar |
| `replenishment/page.tsx` | `REPLENISHMENT_TASKS` | Header Action Toolbar |

### UX Standards
- Nút bấm CSV/XLSX hỗ trợ trạng thái loading, disable khi đang tải để chống click kép.
- Tải file dạng Blob thông qua `api.get(..., { responseType: 'blob' })` và thu hồi `URL.revokeObjectURL(url)` trong `finally`.
- Hiển thị toast lỗi thông báo localized khi thất bại hoặc bị từ chối quyền.

---

## 7. Permission Matrix

| Scope | Thao tác API | Permission yêu cầu |
|---|---|---|
| Master Import | `POST /api/imports/preview`, `POST /api/imports/commit`, `GET /api/imports/errors/*` | `master_data.import` |
| Master Export | `GET /api/imports/template`, `GET /api/exports` | `master_data.export` |
| Ops Export | `GET /api/ops/exports` | `ops.export` |

---

## 8. Execution Packages (7 EPs)

- **EP0: Contract Freeze & Remediation Baseline** — Khóa schema 12 loại Ops Export, cập nhật seed permission và thêm `[Authorize]` + Permission checks cho Master `ImportsController` và `ExportsController`.
- **EP1: Shared Export Utility & Security Sanitizer** — Cập nhật helper xuất CSV/XLSX hỗ trợ sanitize formula injection, UTF-8 BOM, và Header `X-Export-Truncated`.
- **EP2: Master Data Import/Export Parity & Regression Fix** — Chuẩn hóa 4 loại Master IE (UOMS, WAREHOUSES, ZONES, REASONS), sửa lỗi lệch cột template/export.
- **EP3: 12 Ops Export Handlers Implementation** — Triển khai 12 builders trong `OpsExportsController.cs` với `AsNoTracking`, tenant isolation và cap 5.000 dòng.
- **EP4: Frontend Shared Component & 12 Contexts Mount** — Mở rộng `OpsExportButtons.tsx` hỗ trợ 12 types và mount trên 12 trang Admin.
- **EP5: Automated Verifier Script & Integration Tests** — Tạo script `tests/verify_spreadsheet_exports_p46c.ps1` kiểm thử tự động 100% API/CSV/XLSX endpoints.
- **EP6: DBM Verification, Evidence & Program Signoff** — Kiểm thử UI qua browser subagent, chụp ảnh/video evidence lưu tại `planning/evidence/phase_46c_rp45/` và cập nhật tài liệu nghiệm thu.

---

## 9. Tests và Verification

### Automated Verification Command Sequence

```powershell
# 1. Restore & Build Backend Solution
dotnet restore .\Nexustock.sln
dotnet build .\Nexustock.sln --no-restore

# 2. Run Backend Unit & Integration Tests
dotnet test .\tests\Nexustock.MasterData.IntegrationTests\Nexustock.MasterData.IntegrationTests.csproj --no-restore

# 3. Run Dedicated Phase 46C Verifier Script
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_spreadsheet_exports_p46c.ps1

# 4. Frontend Lint & Typecheck
npm --prefix .\frontend run lint
npm --prefix .\frontend exec tsc -- --noEmit
```

---

## 10. Definition of Done (DoD)

- [x] 4/4 Master Data types (UOMS, WAREHOUSES, ZONES, REASONS) pass luồng Import Template -> Preview -> Commit -> Export -> Roundtrip.
- [x] 12/12 Ops Export types pass xuất thành công cả 2 định dạng CSV và XLSX.
- [x] Response Header `X-Export-Truncated` hoạt động chính xác khi dữ liệu vượt 5.000 dòng.
- [x] Sanitize chống CSV Formula Injection hoạt động 100% trên các trường chuỗi bắt đầu bằng `=`, `+`, `-`, `@`.
- [x] 100% endpoint được bảo vệ bởi `[Authorize]` và kiểm tra đúng Permission `master_data.import`, `master_data.export`, `ops.export`.
- [x] 12/12 trang Admin mounted nút `OpsExportButtons` và thực thi tải file blob không lỗi Console/Network.
- [x] Script `verify_spreadsheet_exports_p46c.ps1` chạy thành công 100% không có lỗi.
- [x] Bằng chứng kiểm thử (ảnh/video walkthrough) được lưu đầy đủ tại `planning/evidence/phase_46c_rp45/`.

---

## 11. Rollout và Rollback

- **Rollout:** Apply backend API changes -> Thêm permission nếu thiếu -> Deploy backend -> Deploy frontend.
- **Rollback:** 
  - Backend: Giữ nguyên DB (không thay đổi schema bảng DB). Revert `OpsExportsController.cs` về 4 types cũ nếu phát hiện sự cố.
  - Frontend: Revert `OpsExportButtons.tsx` về phiên bản union 4 types.

---

## 12. Readiness / Approval

| Vai trò | Kết luận | Ngày |
|---|---|---|
| JARVIS | **Module DoD 100% hoàn thành** · strict verifier PASS · 76/76 integration tests · frontend/browser evidence PASS | 2026-07-28 |
| FOUNDER | ☑ Approved · ☑ `rp4` + `rp5` nghiệm thu hoàn tất | 2026-07-28 |
