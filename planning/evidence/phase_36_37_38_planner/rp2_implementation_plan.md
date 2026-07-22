# OBJECTIVE

Đóng **L2-P0** (Phase 36): một engine cấp phát (`AllocateAsync` + `CreatePickTasks`), invariant tồn (interceptor + CHECK `qty_on_hand >= 0`), DF-01 offline MOVE dùng available, CompletePick `RESERVED_UNDERFLOW`.

**Maturity:** `rp1` 100% Ready · `rp2` EP atomic · **`rp3` PASS** (0 blind spot block) — chờ FOUNDER Proceed.

SoT: `d:\1_Project\48_Nexustock\planning\phases\phase_36_inventory_integrity_l2_p0.md` (§20–§22)  
Index: `d:\1_Project\48_Nexustock\planning\function_index_phase36_inventory_integrity.md`

# USER REVIEW REQUIRED

> [!IMPORTANT]
> 1. **Lock vị trí siết hơn** (Allocation bỏ mọi LocationLocks vs GeneratePicks cũ OUTBOUND|ALL).
> 2. **Cùng PR** xóa `OutboundController.GeneratePicks` khi thêm controller Allocation.
> 3. **Port** verify: `$env:NEXUSTOCK_API_URL` hoặc `http://localhost:5024/api`.
> 4. Không execute đến **Proceed P36**.

# OPEN QUESTIONS

> [!NOTE]
> 0 câu hỏi block. OOS P1: WarehouseId Empty; invariant→400 filter.

# ARCHITECTURE OVERVIEW

- **Current:** 2 engine allocate; DF-01; CompletePick thiếu reserved guard; DB thiếu on_hand≥0 check.
- **Target:** GeneratePicks → Allocate(CreatePickTasks=true) cùng TX; interceptor; CHECK; DF-01; RESERVED_UNDERFLOW.
- **Wave:** tự materialize PickTask — **CreatePickTasks phải false**.

# EXECUTION PHASES

## EP0 — CreatePickTasks trong AllocateAsync
- **Goal:** DTO + PickTask cùng TX
- **Risk:** MEDIUM
- **Primary Target Files:**
  - `d:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.Allocation\Dtos\AllocationDtos.cs`
  - `d:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.Allocation\Services\AllocationService.cs`
- **MUST NOT:** Wave `CreatePickTasks=true`; đổi default thành true
- **Steps:**
  1. Thêm `public bool CreatePickTasks { get; set; } = false;` vào `ReserveRequestDto`.
  2. Sau `AllocationReservations.Add(reservation);` (~L180) chèn block copy-paste phase_36 §22.1.
  3. Comment tiếng Việt: cùng TX với reservation.
- **Validation:** `dotnet build backend/Nexustock.Api/Nexustock.Api.csproj`; grep CreatePickTasks trong AllocateAsync trước Commit.
- **Failure Recovery:** Revert 2 file.
- **Continuation:** EP1

## EP1 — OutboundGeneratePicksController + xóa GeneratePicks cũ
- **Goal:** 1 endpoint SoT; FE URL giữ
- **Risk:** HIGH
- **Dependencies:** EP0
- **Primary Target Files:**
  - NEW `d:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.Allocation\Controllers\OutboundGeneratePicksController.cs`
  - `d:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.Inventory\Controllers\OutboundController.cs` DELETE L257–360
- **MUST NOT:** Inventory→Allocation ProjectReference; đổi FE `/outbound/shipments/{id}/generate-picks`
- **Steps:**
  1. Controller `[Route("api/outbound")]` mirror permission helper `AllocationController`.
  2. Inject `IAllocationService`, `InventoryDbContext`, `ITenantProvider`, `IUserPermissionService`.
  3. Permission **`Outbound.Picks.Execute`** (không dùng allocation_reservation.create).
  4. Guard: shipment tồn tại; Status==Open; !PickTasks non-cancelled; AllocateAsync(CreatePickTasks=true, AllowPartial=false, Strategy FEFO|FIFO query).
  5. Catch: InvalidOperation→`INSUFFICIENT_INVENTORY`; KeyNotFound→`SHIPMENT_NOT_FOUND`.
  6. Return `{ message="Sinh pick tasks thành công", shipmentId, status, pickTaskCount }`.
  7. **Cùng commit:** xóa method GeneratePicks cũ (FIFO LotNo).
- **Validation:** build; chỉ 1 GeneratePicks action; grep `OrderBy(i => i.LotNo)` không còn trong path generate-picks.
- **Failure Recovery:** git restore 2 file.
- **Continuation:** EP2

## EP2 — InventoryIntegrityInterceptor + DI
- **Goal:** Chặn SaveChanges qty lệch
- **Risk:** MEDIUM
- **Primary Target Files:**
  - NEW `...\Inventory\Interceptors\InventoryIntegrityInterceptor.cs`
  - NEW `...\Inventory\Exceptions\InventoryInvariantException.cs`
  - `...\Inventory\DependencyInjection.cs`
- **Steps:** Copy §22.3 phase_36 — Singleton **trước** AddDbContext; cả Npgsql + InMemory.
- **Validation:** build.
- **Failure Recovery:** gỡ register.
- **Continuation:** EP3

## EP3 — Migration CHECK qty_on_hand >= 0
- **Goal:** DB constraint bổ sung
- **Risk:** LOW
- **Primary Target Files:** InventoryDbContext + Migration mới
- **Steps:** Pre-check SQL count=0 → Fluent check → `dotnet ef` §22.2 → apply migrate repo-standard.
- **MUST NOT:** Recreate chk reserved/available.
- **Validation:** 3 CHECK trên inventories.
- **Failure Recovery:** DOWN migration.
- **Continuation:** EP4

## EP4 — CompletePick + DF-01 Mobile
- **Goal:** Reserved guard + offline available
- **Risk:** LOW
- **Primary Target Files:**
  - OutboundController.CompletePick (~L363+)
  - MobileController.SyncOffline MOVE (~L209)
- **Steps:**
  1. Trước trừ qty: `QtyReserved < PickedQty` → 400 `RESERVED_UNDERFLOW`.
  2. MOVE: `available = QtyOnHand - QtyReserved`; fail `INSUFFICIENT_QTY:...`.
- **Validation:** code review + EP5 cases.
- **Continuation:** EP5

## EP5 — verify_l2_p0_integrity.ps1 + regression
- **Goal:** Gate tự động
- **Risk:** MEDIUM (data seed)
- **Primary Target Files:** NEW `d:\1_Project\48_Nexustock\tests\verify_l2_p0_integrity.ps1`
- **Steps:**
  1. API base: `$env:NEXUSTOCK_API_URL` ?? `http://localhost:5024/api`.
  2. Login admin (như verify_allocation).
  3. **Seed QC Release** copy wave verify (inbound receive + qc result pass) — **bắt buộc** (BS-R3-02).
  4. Create shipment Open → generate-picks → assert pickTaskCount > 0.
  5. Gọi lại → PICKS_ALREADY_EXIST.
  6. Optional: sync MOVE available fail case nếu fixture sẵn.
  7. Chạy `verify_allocation.ps1`, `verify_wave_picking.ps1`.
- **Validation:** L2 script PASS + 2 regression PASS.
- **Continuation:** EP6

## EP6 — Docs + evidence
- **Goal:** L2 P0 CLOSED
- **Primary:** ACCEPTANCE_L2, phase_36 DoD, IMPLEMENTATION_PLAN row, `planning/evidence/phase_36/`
- **Steps:** Chỉ sau EP5 PASS; re-score Allocation/Inventory; JSON results.
- **Continuation:** DONE → P37

# TEST PLAN SUMMARY

| Test | Command |
|---|---|
| Build | `dotnet build d:\1_Project\48_Nexustock\backend\Nexustock.Api\Nexustock.Api.csproj` |
| L2 P0 | `pwsh d:\1_Project\48_Nexustock\tests\verify_l2_p0_integrity.ps1` |
| Allocation | `pwsh d:\1_Project\48_Nexustock\tests\verify_allocation.ps1` |
| Wave | `pwsh d:\1_Project\48_Nexustock\tests\verify_wave_picking.ps1` |

# ROLLBACK STRATEGY

1. Git revert PR.  
2. EF DOWN chỉ `chk_inventory_balances_qty_on_hand`.  

# DEFINITION OF DONE

phase_36 §14 + §22 BS-R3 = 0 open block + EP0–EP6 done.

# rp3 TRACE

Blind spots đóng: BS-R3-01…18 — xem phase_36 §22. Critic residual 0 block.
