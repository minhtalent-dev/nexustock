# IQC UX Map — GCM Part → Nexustock

**Phase:** 34 · **Ngày khởi tạo:** 2026-07-22  
**Trạng thái:** `rp3` PASS — Ready execute (2026-07-22)  
**SoT phase:** `planning/phases/phase_34_iqc_ux_map_gcm.md` (§18–§20)

---

## 1. Inventory form GCM → đích Nexustock

| # | Form GCM | Actor | Dest Nexustock | API | Gate / Note |
|---|---|---|---|---|---|
| 1 | `frm113_Iqc_Input` | Inspector | `/admin/qc` Result dialog · optional `/mobile/qc` | `POST /api/qc/{lotId}/result` | Pass→Release, Fail→Reject |
| 2 | `frm114_Qc_Input` | QC | Gộp Result dialog | cùng | `qcType` optional STANDARD |
| 3 | `frm114b_Qc_Input` | QC | Gộp Result dialog | cùng | Không tách màn |
| 4 | `frm136_IqcList` | Supervisor | `/admin/qc` queue + filters/aging | `GET /api/qc/queue` | Aging badges |
| 5 | `frm135_IqcOutput` | WH | Outbound / movement **sau** Release | pick/move APIs | `QcGate` bắt buộc |
| 6 | `frm137_IqcInputResult` | Inspector | `/admin/qc/history` + timeline | `GET /api/qc/history`, `.../timeline` | Read-only |
| 7 | `smv_frm6_PartHold` | Auth op | Hold/Release dialog | `POST .../hold\|release` | ReasonCode + permission |
| 8 | `frm108a_Part_Move_FC` | Operator | Mobile movement / Inventory move | move APIs | Block Hold/Unspec/Reject |

### Field notes (điền EP0)

| Form | Field GCM (ghi khi đọc disk) | Field Nexustock | Gap |
|---|---|---|---|
| frm113 | _(EP0 fill)_ | `metrics`, `attachmentRefs`, `samplePlan` | |
| frm136 | _(EP0 fill)_ | `q`, `from`, `to`, `agingHours` | |
| Hold | _(EP0 fill)_ | `reasonCode`, `locationId?` | |

---

## 2. Deliverable checklist

| Artifact | Path | Owner EP |
|---|---|---|
| Phase spec | `planning/phases/phase_34_iqc_ux_map_gcm.md` | — |
| Function index | `planning/function_index_phase34_iqc_ux_map.md` | — |
| UX map (this) | `planning/IQC_UX_MAP_GCM_PART.md` | EP0/EP6 |
| Verify script | `tests/verify_iqc_ux_map.ps1` | EP5 |
| Training sheet | §4 below | EP6 |
| UAT cases | §5 below | EP5 |
| dbm evidence | `planning/evidence/phase_34_dbm/` | EP6 |

---

## 3. Thứ tự triển khai

| Tuần | Việc | DoD tuần |
|---|---|---|
| **W1** | QcGate + wire move/pick/mobile sync | Move Unspec fail; Pass rồi move ok |
| **W2** | Queue filter/aging + history API/UI | List parity IqcList tối thiểu |
| **W3** | Mobile QC (FF) + upload UX | Scan→Pass trên handheld (nếu bật) |
| **W4** | UAT song song GCM + training + dbm | Sign-off QC cutover Parts WH |

---

## 4. Training sheet — Form cũ → nút mới

| Việc quen GCM | Làm trên Nexustock |
|---|---|
| Mở IQC Input | Admin → **QC** → chọn lot → **Ghi kết quả** |
| Xem danh sách IQC | Admin → **QC** → hàng đợi (+ lọc ngày / aging) |
| Xem kết quả cũ | Admin → **QC History** (hoặc timeline lot) |
| Hold nguyên liệu | QC → **Hold** → chọn lý do |
| Unhold / Release | Lookup lot → **Release** → lý do |
| Xuất sau IQC | Chỉ khi lot **Release** → Outbound / Mobile pick |
| Di chuyển khi Hold | **Bị chặn** — hệ thống báo lỗi QC |

---

## 5. UAT cases (tối thiểu 8)

| ID | Case | Expected |
|---|---|---|
| UAT-01 | Lot mới Unspec vào queue | Hiện Pending |
| UAT-02 | Ghi Pass | QcStatus=Release; request Completed |
| UAT-03 | Ghi Fail | QcStatus=Reject; không pick được |
| UAT-04 | Hold lot Release | QcStatus=Hold; move fail |
| UAT-05 | Release từ Hold | QcStatus=Release; move ok |
| UAT-06 | Move khi Unspec | Fail `QC_LOT_NOT_RELEASED` |
| UAT-07 | Pick allocate lot Hold | Không chọn được / fail |
| UAT-08 | Filter aging >24h | Badge/list đúng |
| UAT-09 | Permission thiếu Hold | Forbid |
| UAT-10 | Double submit result | Fail an toàn |
| UAT-11 | (opt) Mobile Pass | Lot Release |
| UAT-12 | Attachment upload | URL lưu trên result |

---

## 6. Không làm (nhắc lại)

VMI · invoice divide · CAP · Ford · wafer · BT1500 desktop clone · tách Part/Shipping product · multi-level QC approve.

---

## 7. Sign-off

| Role | Ký | Ngày |
|---|---|---|
| FOUNDER | | |
| QC Lead | | |
| Warehouse Lead | | |
