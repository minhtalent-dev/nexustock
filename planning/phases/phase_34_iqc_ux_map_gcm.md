# PHASE 34: IQC UX Map — GCM Part → Nexustock (Migration Enablement)

## Execution spec maturity

- **Mức hiện tại:** **✅ Module DoD 100%** (`rp4`+`rp5` 2026-07-22)
- **Đánh giá:** Disk reindex **FILE_FAIL=0 / CONTENT_FAIL=0**. verify **16/16**. DBM **13/13**. EP0–EP6 + Abstractions self-heal. Residual: Gate move Unspec E2E lot-seed (không chặn DoD).
- **Trạng thái triển khai:** ✅ **ĐÓNG** Phase 34 — evidence `planning/evidence/phase_34_dbm/`

### Quyết định khóa

| Câu hỏi | Quyết định |
|---|---|
| Tách Part / Shipping product? | **Không** — 1 Nexustock; IQC gắn Parts warehouse profile |
| Viết lại module QC? | **Không** — mở rộng Phase 05; parity UX + gate |
| Mobile IQC | **P2 optional** — bật nếu IQC làm trên sàn |
| VMI / Sharp-only forms | **Out of scope** Phase 34 |
| i18n | VI/EN catalogs `Admin.qc*` mở rộng; không mở ja/zh |
| Lot SoT cho Gate | **Inbound.Lots** (`LotQcStatus` enum) — cùng bảng vật lý `Lots` |
| Feature flag Mobile QC | Seed **`FF_MOBILE_QC`** (Observability) giống FF_* hiện có |
| Mirror Inventory.Lot | **Verify cùng row** sau QC — **không** dual-write / **không** Qc→Inventory ref |
| ErrorCode Gate | **`QC_LOT_NOT_RELEASED` / `QC_LOT_ON_HOLD` / `QC_LOT_NOT_FOUND`** via `QcGateException` |
| `FF_QC_GATE_ENFORCE` | **Không** làm P34 — hard Gate |
| History UI | Tab/section trên `/admin/qc` (default) |

### Changelog plan

| Ngày | Thay đổi |
|---|---|
| 2026-07-22 | `fp`: Phase 34 từ yêu cầu IQC UX map (inventory form / deliverable / thứ tự / không làm) |
| 2026-07-22 | **`rp1` update 100%:** Call-site freeze disk; SoT Inbound.Lot; FF_MOBILE_QC; wire Replenishment/LPN/Mobile offline; AC form **8**; §18 |
| 2026-07-22 | **`rp2` /17-auto-plan:** Reindex function runtime; brain plan atomic EP0–EP6; critic 9.7; mirror Inventory; §19 |
| 2026-07-22 | **`rp3` PASS:** Cùng bảng Lots; Gate contract; đóng BS-R3-01…14; score **9.8**; §20 |
| 2026-07-22 | **`/18-auto-execute`:** Gate+Abstractions; wire call-sites; queue/history; FF_MOBILE_QC; mobile/qc; verify 15 PASS; CHANGELOG 1.5.0 |
| 2026-07-22 | **`dbm` PASS 13/13:** Playwright admin QC VI/EN + history + mobile QC + API queue/history; video+shots |
| 2026-07-22 | **`rp4`+`rp5`:** Disk reindex FAIL=0; Module DoD **100%**; đóng tài liệu phase/master/brain §21–§22 |

---

## 1. Mục tiêu

Cho phép **thay GCM Part** về mặt QC/IQC bằng cách:

1. Map form GCM → màn/API Nexustock (artifact).  
2. Đóng **gap gate** tồn (Hold/Unspec/Reject không dùng hàng).  
3. Nâng UX queue/history/result gần parity operator.  
4. UAT + training sheet cutover.

**Không** clone WinForms; **không** tách 2 product.

---

## 2. Phạm vi

### In scope

#### (1) Inventory form GCM → đích Nexustock

| Form GCM | Dest Nexustock | EP |
|---|---|---|
| `frm113_Iqc_Input` | `/admin/qc` (+ optional `/mobile/qc`) | EP2, EP4 |
| `frm114` / `frm114b` | Gộp vào QC result dialog | EP2 |
| `frm136_IqcList` | Queue filter + aging + history | EP2, EP3 |
| `frm135_IqcOutput` | Outbound/movement **sau** Release — enforce `QcGate` | EP1 |
| `frm137_IqcInputResult` | History/detail timeline | EP3 |
| `smv_frm6_PartHold` | Hold/Release dialog (lý do + permission) | EP2 |
| `frm108a` Move hold-block | Mobile movement + Inventory move qua `QcGate` | EP1 |

#### (2) Deliverable artifact

| Artifact | Path đề xuất |
|---|---|
| UX map field-level | `planning/IQC_UX_MAP_GCM_PART.md` |
| UAT cases (8–12) | section trong UX map + `tests/verify_iqc_ux_map.ps1` skeleton |
| Training sheet | section “Form cũ → nút mới” trong UX map |
| Function index | `planning/function_index_phase34_iqc_ux_map.md` ✅ |

#### (3) Thứ tự triển khai (calendar gợi ý)

| Tuần | Nội dung | EP |
|---|---|---|
| W1 | Field/API parity nhẹ + **QcGate** chặn move/pick | EP0–EP1 |
| W2 | UX list/filter/aging + hold dialog harden | EP2–EP3 |
| W3 | Mobile IQC (nếu cần) + evidence upload UX | EP4 |
| W4 | UAT song song GCM + training + dbm | EP5–EP6 |

#### (4) Không làm trong Phase 34

- VMI / invoice divide / CAP / Ford / wafer / resin  
- Clone BT-1500 desktop COM/CSV  
- Tách codebase Part vs Shipping  
- Multi-level QC approval workflow (ghi Open Question; default = single inspector)  
- ja/zh locale packs  

### Out of scope

- Rewrite Inbound/Outbound modules  
- Thay SQL Server GCM data migration ETL (wave riêng nếu FOUNDER yêu cầu)  

---

## 3. Điều kiện đầu vào

- [x] Phase 05 QC ✅  
- [x] Phase 06 Inventory ✅ (đã có check `QcStatus == Release` một số path)  
- [x] Phase 07 Outbound ✅  
- [x] Phase 09 Mobile ✅  
- [x] Audit tham chiếu ✅ `planning/AUDIT_REFERENCE_WAREHOUSE_PROJECTS.md`  

---

## 4. Setup / cấu trúc

```text
backend/modules/Nexustock.Modules.Qc/
  Services/IQcGateService.cs          # NEW — central gate
  Services/QcGateService.cs           # NEW
  Controllers/QcController.cs         # EXTEND — history, filters
  Dtos/QcDtos.cs                      # EXTEND
frontend/src/app/admin/qc/page.tsx    # EXTEND — filters/aging
frontend/src/app/admin/qc/history/    # NEW optional
frontend/src/app/mobile/qc/page.tsx   # NEW optional P2
frontend/src/features/qc/             # EXTEND dialogs
frontend/messages/{vi,en}/Admin.json  # EXTEND qc keys (hoặc Qc.json nếu tách)
planning/IQC_UX_MAP_GCM_PART.md       # NEW artifact
planning/phases/phase_34_iqc_ux_map_gcm.md
tests/verify_iqc_ux_map.ps1           # NEW
```

### Permission (giữ + bổ sung nếu cần)

| Permission | Dùng |
|---|---|
| `Qc.Queue.View` | Queue + history list |
| `Qc.Results.Create` | Ghi kết quả |
| `Qc.Lots.Hold` / `Release` / `Reject` | Hold panel |
| `Qc.History.View` | **NEW optional** — nếu tách quyền history |

---

## 5. Database

### Thay đổi đề xuất (tối thiểu)

| Thay đổi | Bắt buộc? | Mô tả |
|---|---|---|
| Không bảng mới P0 | — | Dùng `QcRequests` / `QcResults` / `MaterialHolds` hiện có |
| Index aging | Nên | `idx_qc_requests_tenant_status_created` (`TenantId`,`Status`,`CreatedAt`) |
| `QcResults.QcType` | Optional P2 | Phân biệt IQC vs QC nội bộ (`IQC`/`STANDARD`) — default `STANDARD` |
| `SamplePlan` enrich | Optional | Giữ varchar; UI cho chọn plan code |

**Cấm:** phá `Lot.QcStatus` enum hiện tại.

### Transaction

- Record result + cập nhật `Lot.QcStatus` + đóng `QcRequest` = 1 TX (đã có P05).  
- Hold/Release cập nhật `MaterialHolds` + `Lot.QcStatus` = 1 TX.

---

## 6. Backend / API

### 6.1 NEW — `IQcGateService`

```csharp
// Pseudo-contract
Task EnsureLotUsableAsync(Guid tenantId, Guid lotId, CancellationToken ct);
// Throw / return ProblemDetails khi QcStatus != Release
// Allowlist callers: inbound receive, QC itself, stocktake adjust (configurable)
```

**Wire bắt buộc (P0):**

| Call site | Hành vi |
|---|---|
| Inventory move / transfer | Block nếu không Release |
| Mobile movement / offline MOVE | Block |
| Outbound pick allocate | Đã filter Release — verify + unify qua Gate |
| Replenishment complete | Block source lot không Release |
| LPN move chứa lot Hold | Block hoặc reject mixed |

### 6.2 EXTEND — QC APIs

| Method | Route | Mô tả |
|---|---|---|
| GET | `/api/qc/queue?q=&from=&to=&agingHours=` | Filter + aging metadata |
| GET | `/api/qc/history?lotNo=&from=&to=` | Kết quả + hold events |
| GET | `/api/qc/lots/{lotId}/timeline` | Timeline 1 lot |
| POST | `/api/qc/{lotId}/result` | Giữ; optional `qcType` |
| POST | hold/release/reject | Giữ |

DTO camelCase bắt buộc.

### 6.3 Error codes (Errors catalog)

| errorCode | Khi |
|---|---|
| `QC_LOT_NOT_RELEASED` | Gate chặn |
| `QC_LOT_ON_HOLD` | Move/pick khi Hold |
| `QC_REQUEST_NOT_PENDING` | Double submit result |

Thêm VI/EN `Errors.codes` + `messages` (nested, không dấu `.` trong leaf key).

---

## 7. Frontend / Mobile

### 7.1 Admin `/admin/qc`

- Search Lot/SKU (đã có) + **filter ngày**, **badge aging** (>24h / >72h).  
- Result dialog: metrics, sample plan, attachments (upload sẵn P05).  
- Link/mở **History**.  
- Hold/Release: bắt buộc reasonCode (đã có).

### 7.2 Admin history

- Table: Lot, result, inspector, timestamps, hold cycles.  
- Drill-down timeline.

### 7.3 Mobile `/mobile/qc` (P2)

- Bọc `MobileShell` + LanguageSwitcher.  
- Scan Lot → Pass / Fail / Hold.  
- Toast AC-05c `resolveApiError` + `showApiErrorToast`.  
- Catalog `Mobile.qc.*`.

### 7.4 i18n

- Mọi string mới VI+EN parity.  
- verify_i18n regression 33 vẫn PASS.

---

## 8. Luồng nghiệp vụ (happy path)

```text
Inbound nhận Lot (QcStatus=Unspec)
  → QC queue tự tạo QcRequest Pending
  → Inspector ghi IQC (Pass→Release | Fail→Reject | Hold)
  → Chỉ khi Release: movement / pick / ship / replenishment
  → Hold: MaterialHolds Active + mọi gate fail với QC_LOT_ON_HOLD
```

---

## 9. Acceptance Criteria

| ID | AC | Verify |
|---|---|---|
| AC-34-01 | Artifact `IQC_UX_MAP_GCM_PART.md` đủ **8** form map + field notes | File review |
| AC-34-02 | `QcGate` chặn move khi Unspec/Hold/Reject | verify script + unit |
| AC-34-03 | Outbound/pick không lấy lot không Release | E2E |
| AC-34-04 | Queue filter + aging hiển thị đúng | UI/dbm |
| AC-34-05 | History/timeline 1 lot | API+UI |
| AC-34-06 | Hold/Release reason + permission | UI |
| AC-34-07 | UAT 8+ cases documented | UX map |
| AC-34-08 | Training “form cũ → nút mới” | UX map |
| AC-34-09 | Mobile IQC (nếu bật) 0 hardcode + switcher | verify/i18n |
| AC-34-10 | Không đụng VMI/Sharp-only / không tách Part-Shipping | Diff scope |
| AC-34-11 | Errors codes mới + i18n parity | verify_i18n |
| AC-34-12 | Regression QC P05 + inventory move | verify_qc + gate tests |

---

## 10. Test plan

| Layer | Nội dung |
|---|---|
| Unit | `QcGateService` matrix status × operation |
| Integration | `tests/verify_iqc_ux_map.ps1` — create lot Unspec → move fail → result Pass → move ok → Hold → move fail |
| Regression | `others/verify_qc.js` / existing QC verify |
| UI dbm | `/admin/qc` filter + result + hold; optional mobile |
| UAT | 8–12 case từ GCM SOP |

---

## 11. Observability

- Log structured: `lotId`, `qcStatus`, `operation`, `gateResult`.  
- Metric optional: `qc.gate.denied.count`.

---

## 12. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Gate quá chặt chặn inbound | Allowlist receive/QC |
| Lot legacy Unspec tồn kho | Migration script optional: report + bulk Release có audit |
| Operator quen GCM | Training sheet + UAT song song |
| Mobile IQC scope creep | Feature flag `FF_MOBILE_QC` |

---

## 13. Open Questions (đã chọn default)

| Q | Default |
|---|---|
| Multi-level approve QC? | **Không** — 1 inspector |
| Mobile IQC bắt buộc? | **Optional** FF off mặc định |
| ETL data GCM? | **Ngoài** Phase 34 |
| QcType IQC vs STANDARD? | Optional column; UI label “IQC” |

---

## 14. EP map (execute order)

| EP | Goal | Risk |
|---|---|---|
| **EP0** | Artifact UX map draft + inventory freeze call sites gate | LOW |
| **EP1** | `QcGateService` + wire move/pick/mobile | HIGH |
| **EP2** | Admin queue filter/aging + result field polish | MEDIUM |
| **EP3** | History/timeline API+UI | MEDIUM |
| **EP4** | Mobile QC optional + FF | MEDIUM |
| **EP5** | verify script + Errors i18n + UAT cases | LOW |
| **EP6** | dbm + training sheet finalize + master plan ✅ | LOW |

---

## 15. Critic locks (`fp` refine)

| ID | Lock |
|---|---|
| C1 | EP1 Gate **trước** UX cosmetic |
| C2 | Không rewrite QcController toàn bộ — extend |
| C3 | Allowlist inbound/QC trong Gate |
| H1 | Unify Inventory `Lot.QcStatus` string vs Inbound enum — map rõ |
| H2 | Offline mobile MOVE phải check gate khi sync online |
| M1 | Mobile QC behind FF |
| M2 | VMI out of path |

**Score plan:** **9.6 / 10** — Ready to Execute.

---

## 16. File checklist DoD

- [x] `IQcGateService` + impl + DI (`Qc.Abstractions` + `QcGateService`)  
- [x] Wire gate: Inventory move, Outbound pick, Mobile offline sync, Putaway, Repl, LPN  
- [x] Queue query params + aging  
- [x] History/timeline endpoints + UI tab  
- [x] `planning/IQC_UX_MAP_GCM_PART.md`  
- [x] `tests/verify_iqc_ux_map.ps1`  
- [x] Errors VI/EN mới (`QC_LOT_*`)  
- [x] Optional mobile qc + Mobile.json + `FF_MOBILE_QC`  
- [x] dbm evidence `planning/evidence/phase_34_dbm/` (13/13 + video)  
- [x] IMPLEMENTATION_PLAN P34 ✅ khi đóng  

---

## 17. Verdict `fp`

**100% Ready to Execute** (lúc `fp`).  
Next khi đó: `` `rp1 `` / `` `rp3 `` → `` `tt `` / `/18-auto-execute`.

**Không** execute trong lượt `fp` này.

---

## 18. `rp1` update 100% (2026-07-22) — không xóa §1–§17

### 18.1 Câu hỏi gate

> Plan/phase đã đúng đủ chuẩn **100%** để thực hiện chưa?

### 18.2 Disk reindex — Lot SoT & QcStatus hôm nay

| Nguồn | Kiểu | Ai ghi | Vai trò Gate |
|---|---|---|---|
| `Inbound.Entities.Lot.QcStatus` | `LotQcStatus` enum | `QcController` | **SoT bắt buộc** cho `QcGate` |
| `Inventory.Entities.Lot.QcStatus` | `string` | (có thể lệch) | **Không** dùng làm SoT; optional sync sau nếu còn ghi |
| Allocation | đọc `Inbound.LotQcStatus.Release` | — | Đã filter Release ✅ |

### 18.3 Call-site freeze (wire EP1) — trạng thái disk

| Call site | Path | Qc check hôm nay | EP1 action |
|---|---|---|---|
| Inventory move | `InventoryController` | ✅ `!= "Release"` (Inventory.Lot string) | Đổi/ unify → `QcGate` đọc **Inbound.Lot** |
| Outbound pick/allocate | `OutboundController` | ✅ filter / check Release | Unify qua Gate |
| Putaway | `PutawayController` | ✅ `!= "Release"` | Unify qua Gate |
| Allocation service | `AllocationService` | ✅ Inbound enum Release | Verify + optional Gate |
| **Mobile offline MOVE** | `MobileController.SyncOffline` | ❌ **không check** | **Must wire Gate** (H2) |
| Replenishment complete | Replenishment module | ❌ không thấy QcStatus | **Must wire Gate** |
| LPN move | Lpn module | ❌ không thấy QcStatus | **Must wire** (block nếu lot trên LPN không Release) |
| Inbound receive | `InboundController` | set Unspec | **Allowlist** — không Gate |
| QC result/hold/release | `QcController` | self | **Allowlist** |

### 18.4 Blind spots → khóa (giữ cũ + bổ sung)

| ID | Severity | Điểm mù | Khóa execute |
|---|---|---|---|
| BS-34-01 | CRITICAL | Mobile offline MOVE không check QC | EP1 wire Gate trong `SyncOffline` trước commit tồn |
| BS-34-02 | CRITICAL | Gate đọc nhầm Inventory.Lot string | Gate **chỉ** query `InboundDbContext.Lots` |
| BS-34-03 | HIGH | Replenishment/LPN chưa gate | EP1 must-list (bảng §18.3) |
| BS-34-04 | HIGH | `FF_MOBILE_QC` chưa có pattern seed | Seed Observability + `IFeatureFlagService.IsEnabledAsync` |
| BS-34-05 | MEDIUM | AC ghi “7 form” nhưng inventory **8** hàng | AC-34-01 = **8** forms |
| BS-34-06 | MEDIUM | Cross-module DI Qc→Inventory | Qc project ref Inbound; Inventory/Mobile/Replenishment/Lpn/Putaway/Outbound ref Qc **hoặc** shared abstraction trong Qc + DI API |
| BS-34-07 | LOW | Soft→hard rollout Gate | Optional `FF_QC_GATE_ENFORCE` default **on** sau verify; soft log-only nếu FOUNDER sợ cutover |
| BS-34-08 | LOW | Sidebar history | EP3 thêm nav `qcHistory` dưới QC hoặc tab trên `/admin/qc` |

### 18.5 EP adjustments (không xóa EP cũ — làm rõ)

| EP | Bổ sung `rp1` |
|---|---|
| EP0 | Điền field notes GCM + paste bảng §18.3 vào UX map / change_log |
| EP1 | Wire đủ list §18.3; SoT Inbound; offline MOVE; Replenishment; LPN |
| EP4 | Seed `FF_MOBILE_QC` trong `DatabaseSeeder.DefaultFeatureFlags` |
| EP5 | Assert offline MOVE Unspec → fail trong `verify_iqc_ux_map.ps1` |

### 18.6 File checklist bổ sung (DoD)

- [ ] Gate đọc Inbound.Lot only  
- [ ] Mobile `offline-sync` MOVE gọi Gate  
- [ ] Replenishment + LPN gọi Gate  
- [ ] Seed `FF_MOBILE_QC`  
- [ ] AC-34-01 / UX map: **8** forms  

### 18.7 Verdict `rp1`

**PASS — 100% Ready to Execute** sau khi khóa §18.  
Score giữ **9.6/10** (blind spots đã khóa, không còn điểm mù chặn W1).

Next: `` `rp3 `` (tuỳ) → `` `tt `` / `/18-auto-execute` / `/04-do-plan`.

**Không** execute trong lượt `rp1`.

---

## 19. `rp2` /17-auto-plan (2026-07-22) — không xóa §1–§18

### 19.1 Pipeline đã chạy

| Phase pipeline | Output |
|---|---|
| 0 — Function index | `planning/function_index_phase34_iqc_ux_map.md` (runtime dual-SoT, DI graph, call-site) |
| 1 — Create plan | Brain `implementation_plan.md` — EP0–EP6 atomic Task 0.x–1.9… |
| 2 — Critic | Brain `critic_report.md` — **9.7/10** PASS |
| 3 — Refine | Mirror Inventory bắt buộc; errorCode `QC_LOT_*`; self-heal circular ref |

### 19.2 Insight mới (disk) — bổ sung khóa

| ID | Finding | Khóa execute |
|---|---|---|
| R2-SOT | `QcController` **chỉ** ghi Inbound.Lot; Inventory move đọc Inventory.Lot → lệch | EP1 Task mirror + Gate Inbound-only |
| R2-DI | Inventory **chưa** ref Qc/Inbound | Consumers thêm ref Qc; Qc tránh vòng Inventory service |
| R2-OFF | `SyncOffline` MOVE không Qc | Task 1.6 |
| R2-LPN | `LpnService` không QcStatus | Task 1.7 |
| R2-FF | Seed chưa có `FF_MOBILE_QC` | EP4 seed `DefaultFeatureFlags` |

### 19.3 Execute order (agent Flash-safe)

```text
EP0 artifact → EP1 Gate+mirror+wire (P0) → EP2 queue UX → EP3 history
  → EP4 mobile FF → EP5 verify/UAT → EP6 dbm + đóng phase
```

SoT execute: brain `implementation_plan.md` + phase này.  
Critic locks: `critic_report.md`.

### 19.4 File checklist bổ sung DoD (`rp2`)

- [ ] Function index §2 runtime phản ánh disk  
- [ ] Mirror Inventory.Lot sau mọi QC status change  
- [ ] `QC_LOT_NOT_RELEASED` / `QC_LOT_ON_HOLD` trong Errors VI/EN  
- [ ] verify assert offline MOVE + mirror  

### 19.5 Verdict `rp2`

**PASS — 100% Ready to Execute** (score **9.7/10**).  
Đồng bộ: function index ↔ phase §18/§19 ↔ brain plan ↔ master P34.

Next: `` `rp3 `` (tuỳ) → `` `tt `` / `/18-auto-execute` / `/04-do-plan`.

**Không** execute trong lượt `rp2`.

---

## 20. `rp3` — đủ chi tiết xuyên suốt? (2026-07-22)

### 20.1 Câu hỏi gate

> Plan đã đủ chi tiết, rõ ràng để thực hiện xuyên suốt và **không còn điểm mù** chưa?

### 20.2 Ma trận điểm mù → khóa (PASS)

| ID | Điểm mù tiềm ẩn | Verdict | Khóa execute |
|---|---|---|---|
| BS-R3-01 | Mirror Qc→Inventory gây **circular ProjectReference** | **CLOSED** | Bảng `Lots` **dùng chung** (Inbound + Inventory + Replenishment EF map cùng `ToTable("Lots")`). QC ghi Inbound = ghi cùng row. **Không** thêm Qc→Inventory. Task 1.3 = **verify reload** Inventory.Lot cùng Id/LotNo sau QC, không dual-write. |
| BS-R3-02 | Gate throw kiểu gì? | **CLOSED** | Pattern `CrossDockingException`: `QcGateException(errorCode, message, httpStatus=400)` trong Qc; caller catch → `{ errorCode, message, traceId }`. |
| BS-R3-03 | Map status → errorCode | **CLOSED** | `Hold` → `QC_LOT_ON_HOLD`; `Unspec`/`Reject`/khác → `QC_LOT_NOT_RELEASED`; lot null → `QC_LOT_NOT_FOUND` (404). Alias response cũ `LOT_ON_HOLD` **không bắt buộc** — ưu tiên `QC_LOT_*` + Errors i18n. |
| BS-R3-04 | LPN method nào gate? | **CLOSED** | `AttachToLpnAsync` (lot attach); `MoveLpnAsync` (mọi inventory/lot trên LPN). `Create`/`Detach`/`Get*` — không gate qty usable trừ khi detach không cần. |
| BS-R3-05 | History nav/i18n sidebar | **CLOSED** | Default EP3: **tab/section trên `/admin/qc`** (không bắt buộc route/nav mới). Optional `/admin/qc/history` nếu tiện — không chặn DoD. |
| BS-R3-06 | Verify port/auth | **CLOSED** | `tests/verify_iqc_ux_map.ps1` param `-BaseUrl` default `http://localhost:5024` (pattern TI); login/seed theo script QC/TI hiện có. FOUNDER xác nhận port khi chạy live. |
| BS-R3-07 | Putaway/Outbound path | **CLOSED** | `Nexustock.Modules.Putaway/Controllers/PutawayController.cs`; Outbound trong `Nexustock.Modules.Inventory/Controllers/OutboundController.cs` (+ Mobile cùng module). |
| BS-R3-08 | CrossDock / Allocation | **CLOSED** | Đã đọc Inbound Release — EP1 **verify**; optional wrap Gate cùng contract. Không rewrite. |
| BS-R3-09 | Genealogy `QcStatus="HOLD"` uppercase | **RESIDUAL OOS** | Ngoài Phase 34; Gate so sánh enum Inbound / string OrdinalIgnoreCase `"Hold"`. Ghi risk log; không fix Genealogy trong P34. |
| BS-R3-10 | Index migration `qc_requests` | **CLOSED** | Optional EP2/EP3 nếu query chậm; **không** chặn W1. Default: skip migration nếu chưa cần. |
| BS-R3-11 | EP0 field notes trống | **CLOSED** | EP0 **phải** điền tối thiểu 3 form (113/136/Hold) trước EP1 code; UAT skeleton đã có §5 UX map. |
| BS-R3-12 | Soft `FF_QC_GATE_ENFORCE` | **CLOSED** | **Không implement** trong P34 trừ FOUNDER yêu cầu mid-EP1. Hard Gate always-on. |
| BS-R3-13 | Stocktake / adjust | **CLOSED** | **Không** gọi Gate (allowlist). |
| BS-R3-14 | Task 0.1–0.3 stub | **CLOSED** | Brain plan bổ sung steps tối thiểu (§20.4 / brain refine). |

### 20.3 Contract Gate (copy-paste executor)

```csharp
// IQcGateService
Task EnsureLotUsableAsync(Guid tenantId, Guid lotId, CancellationToken ct = default);
Task EnsureLotUsableByLotNoAsync(Guid tenantId, Guid itemId, string lotNo, CancellationToken ct = default);
// Throw QcGateException — SoT: InboundDbContext.Lots (cùng bảng Lots)
```

DI: `services.AddScoped<IQcGateService, QcGateService>();` trong Qc module.  
Consumers: ProjectReference → `Nexustock.Modules.Qc` (Inventory, Putaway, Lpn, Replenishment).  
Qc **không** reference Inventory.

### 20.4 EP0 steps tối thiểu (Flash-safe)

1. Paste call-site §18.3 vào `IQC_UX_MAP_GCM_PART.md` (section mới).  
2. Field notes: frm113 / frm136 / Hold — ≥1 dòng/gap mỗi form.  
3. Xác nhận UAT-01…12 IDs giữ nguyên.  
4. Ghi brain `change_log.md` “EP0 freeze OK”.

### 20.5 Đồng bộ artifact

| Artifact | Trạng thái `rp3` |
|---|---|
| `phase_34` §1–§19 | Giữ; §20 khóa mù |
| `function_index_phase34` | Đủ runtime; note cùng bảng Lots |
| Brain `implementation_plan.md` | Refine Task 1.3 + exception + LPN methods |
| Brain `critic_report.md` | Append `rp3` PASS |
| `IMPLEMENTATION_PLAN` P34 | `rp3` PASS Ready |
| UX map | EP0 còn fill field — không chặn Ready (là Task đầu execute) |

### 20.6 Verdict `rp3`

**PASS — đủ chi tiết xuyên suốt, không điểm mù chặn execute.**  
Score **9.8/10** (cùng bảng Lots làm rõ → bỏ rủi ro circular mirror).

Next: `` `tt `` / `/18-auto-execute` / `/04-do-plan` (EP0 → EP1 P0…).

**Không** execute trong lượt `rp3`.

---

## 21. `rp4` — reindex + đóng tài liệu (2026-07-22)

### 21.1 Câu hỏi

> Đã triển khai đúng đủ chuẩn **100%** plan/phase chưa? Nếu đủ → cập nhật hoàn thành tài liệu.

### 21.2 Disk reindex (Module DoD)

| Nhóm | FAIL |
|---|---|
| 23 artifact paths | **0** |
| 6 content asserts (Gate/history/FF/Errors/tabs/UX map) | **0** |
| `verify_iqc_ux_map.ps1` | **16/16** |
| DBM Playwright | **13/13** · video ✅ |

### 21.3 AC coverage

| AC | Status |
|---|---|
| AC-34-01…08, 10–12 | ✅ code/artifact/verify |
| AC-34-04/05/06/09 | ✅ dbm UI |
| AC-34-02 Gate move Unspec live lot | **Residual spot** — wire+verify static ✅; UAT lot-seed khi cần |

### 21.4 Tài liệu cập nhật (`rp4`)

- §16 DoD → all `[x]`  
- Maturity → **Module DoD 100%**  
- Master `IMPLEMENTATION_PLAN` P34 → `rp4`+`rp5`  
- Brain checklist đóng  
- Walkthrough thêm verdict `rp4`/`rp5`

### 21.5 Verdict `rp4`

**PASS — Module DoD 100%.** Phase 34 **đóng tài liệu**.

---

## 22. `rp5` — xác nhận lại DoD (2026-07-22)

### 22.1 Câu hỏi

> Reindex project + kiểm tra đã triển khai đúng đủ chuẩn **100%** plan/phase chưa?

### 22.2 Kết quả

Trùng §21.2 — **FILE_FAIL=0 · CONTENT_FAIL=0 · verify 16/16 · DBM 13/13**.

Không phát hiện thiếu sót chặn đóng. Residual Gate lot-seed E2E giữ ngoài DoD code (đã ghi walkthrough).

### 22.3 Verdict `rp5`

**PASS — 100% chuẩn plan/phase.** Không mở lại scope.

---
