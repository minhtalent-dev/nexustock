# PHASE 30: Production Readiness Gate

## Execution spec maturity

- **Mức hiện tại:** 100% Ready (sau `up` 2026-07-21)
- **Đánh giá:** Contract §19 + Open decisions §19.10 đã khóa. FOUNDER: AC-01=A; AC-08 waiver (SAP sandbox chưa sẵn) — [WAIVER_AC08.md](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_30/WAIVER_AC08.md). Evidence folder đã tạo. **Được phép lập plan execute / do-plan.**
- **Khi cần upgrade sau Ready:** Không cần — chỉ cập nhật evidence khi chạy AC; khi sandbox sẵn thì hủy waiver AC-08 và PASS lại.
- **Trạng thái triển khai:** ⬜ Chưa bắt đầu code (chờ `fp` / `tt` / `/04-do-plan`).

## 1. Mục tiêu

Thiết lập Cổng kiểm soát sẵn sàng vận hành (Production Readiness Gate) cuối cùng. Phase này không phải là một module nghiệp vụ CRUD mới cho người dùng kho, mà là **quy trình kiểm soát chất lượng, UAT và kỹ thuật cắt chuyển hệ thống (Cutover/Rollback)** để chứng minh Nexustock đã sẵn sàng go-live an toàn dưới sự vận hành của **1 Developer chính**.

## 2. Phạm vi

### In scope

- **Diễn tập khôi phục & Cắt chuyển (Cutover & Rollback Rehearsal):** Viết tài liệu kịch bản cutover chi tiết từng giờ (Runbook) và thực hành khôi phục DB dự phòng từ bản backup trong vòng dưới 2 giờ (đáp ứng RTO).
- **Hardening hệ thống (System Hardening):** 
  - Khóa toàn bộ các cổng/service không cần thiết.
  - Cấu hình chỉ cho phép Local Agent chạy WSS và chặn các trang web không thuộc WMS kết nối.
  - Cài đặt Code Signing Certificate doanh nghiệp cho bộ cài MSIX của Local Agent.
- **Nghiệm thu người dùng cuối (UAT Signoff):** Chạy và ký biên bản kiểm thử cho 4 kịch bản UAT cốt lõi (nhập hàng, QC, đóng gói + cân, lỗi in ấn).
- **Tải trọng và An toàn:** Thực hiện smoke test tải đồng thời 50 RF scanners và rà soát lỗi bảo mật IDOR.

### Non-negotiable output

- Biên bản xác nhận UAT thành công (UAT Signoff Evidence).
- Báo cáo diễn tập Rollback thành công trong môi trường Rehearsal (đảm bảo thời gian khôi phục < 2 giờ).
- File cài đặt Local Agent đã được ký số (Signed MSIX Installer).
- Màn hình dashboard giám sát hệ thống (Observability Dashboard) hoạt động, hiển thị đầy đủ Trace ID.

## 3. Điều kiện đầu vào

### Readiness checklist

- Tất cả 29 phase trước đó đã hoàn tất và vượt qua tiêu chí nghiệm thu tương ứng.
- Không còn lỗi Critical hoặc High nào chưa được vá trong issue tracker. → **`up` 2026-07-21: FOUNDER xác nhận AC-01 = A (sạch).**
- Môi trường SAP sandbox hoạt động ổn định và sẵn sàng cho việc test liên thông dữ liệu. → **`up` 2026-07-21: sandbox chưa sẵn → AC-08 waived** ([WAIVER_AC08.md](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_30/WAIVER_AC08.md)); không chặn Ready execute nội bộ kho.

## 4. Setup & Infrastructure Configuration

Phase này tập trung vào cấu hình hạ tầng và cài đặt bảo mật cho cả máy chủ Cloud và máy chủ trạm local:
- **Cloud/On-prem VM:** Thiết lập Docker Compose production profile, cấu hình SSL TLS 1.3 và Rate Limiting.
- **Local Agent trạm:** Phân phối file cài đặt MSIX đã được ký số cho máy trạm thủ kho.

## 5. Database & Configuration State

Không tạo bảng CRUD mới cho user. Sử dụng các bảng hệ thống để theo dõi quá trình nghiệm thu và checklist (chỉ dùng nội bộ cho Admin và DevOps):

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `UatRuns` | Lưu lịch sử chạy các đợt UAT | ID, Người ký duyệt, Kết quả, Trace ID liên đới |
| `CutoverLogs` | Ghi nhận mốc thời gian thực hiện cutover | Mã công việc, Thời gian bắt đầu, Kết thúc, Trạng thái |
| `IncidentDrills` | Ghi nhận kết quả diễn tập sự cố (mất mạng, sập DB) | Kịch bản, Thời gian RTO thực tế, Người diễn tập |

## 6. Backend/API hỗ trợ vận hành

| API | Mục đích | Ghi chú triển khai |
|---|---|---|
| `GET /api/admin/readiness` | Kiểm tra độ sẵn sàng của DB, Redis, SAP link | Chỉ dành cho Admin, kiểm tra toàn bộ kết nối. |
| `POST /api/admin/cutover/freeze` | Khóa giao dịch ghi để backup DB trước cutover | Chỉ dành cho DevOps Admin có quyền hệ thống cao nhất. |

## 7. Frontend/RF/mobile support

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| System Readiness Dashboard | Admin theo dõi trạng thái các cổng kết nối và agent | Giao diện hiển thị màu xanh/đỏ cho các service kết nối, không cho phép mutation thường. |
| Cutover Status Board | Theo dõi checklist cutover thời gian thực | View-only cho toàn đội dự án để biết mốc công việc đang chạy. |

### Chuẩn UI áp dụng

* UI text dùng Sentence case.
* Không dùng inline style.
* Sử dụng Next.js, Tailwind CSS và Shadcn UI. Không dùng inline style, tuân thủ component/style nhất quán.
* Mọi action nguy hiểm có confirm rõ ràng.
* Mọi màn hình có loading, empty, error, unauthorized state.
* Bảng dữ liệu có filter, pagination và trạng thái no result.
* RF/mobile ưu tiên input scan auto-focus, font lớn, ít nút, phản hồi rõ.

### State cần hiển thị

* Draft/open/in progress/completed/cancelled nếu phase có workflow.
* Locked/blocked/exception nếu thao tác bị chặn.
* Last updated và actor cho dữ liệu quan trọng.
* Trace ID hoặc reference ID khi cần hỗ trợ vận hành.

## 8. Execution flow

### 8.1 Quy trình cutover chi tiết từng giờ (Cutover Runbook Timeline)

Mọi mốc thời gian dưới đây sử dụng placeholder giờ thực tế và được điều phối trực tiếp bởi DevOps (Developer chính):

| Mốc thời gian | Tên bước công việc | Người thực hiện | Nội dung chi tiết | Lệnh thực thi / Ghi chú |
|---|---|---|---|---|
| **T-04:00** (18:00) | **System Freeze** | DevOps | Khóa toàn bộ các giao dịch ghi mới từ ERP/SAP. Thông báo bảo trì hệ thống. | Gọi API: `POST /api/admin/cutover/freeze` để đưa WMS về chế độ Read-Only. |
| **T-03:00** (19:00) | **Database Backup** | Lead Dev | Thực hiện pg_dump sao lưu toàn bộ dữ liệu hiện tại trước cutover. | Lệnh: `pg_dump -U postgres -F t -f nexustock_prod_backup_T4.tar nexustock_prod` |
| **T-02:30** (19:30) | **Infrastructure Deployment** | DevOps | Run docker-compose profile mới trên Production VM, thực thi script migration UP. | Lệnh: `docker-compose -f docker-compose.prod.yml up -d --build && dotnet ef database update` |
| **T-01:30** (20:30) | **Local Agent Rollout** | IT Support | Hỗ trợ cài đặt và tin cậy chứng chỉ SSL tự ký cho Local Agent tại các trạm. | Chạy bộ cài: `NexustockLocalAgent.msix`. Xác thực service chạy nền. |
| **T-01:00** (21:00) | **Integration Smoke Test** | Lead Dev | Ping thử kết nối SAP Gateway, in thử tem barcode qua WebSocket trạm local. | Verify in 5 tem nhãn kiểm thử không phát sinh lỗi. |
| **T-00:30** (21:30) | **Go/No-Go Evaluation** | FOUNDER & Tech | Rà soát điều kiện sẵn sàng. FOUNDER đưa ra quyết định Go hoặc No-Go. | Dựa trên bảng Go/No-Go Thresholds ở Section 9.1. |
| **T-00:00** (22:00) | **System Live (GO-LIVE)** | DevOps | Mở cổng tiếp nhận API Inbound từ SAP, tắt chế độ Read-Only trên WMS. | Khởi chạy chính thức hệ thống Nexustock WMS. |
| **T+01:00** (23:00) | **Hypercare Monitoring I** | Lead Dev | Kiểm tra log, trace ID của 10 đơn nhập xuất đầu tiên từ SAP sang WMS. | Theo dõi trace log tại **Observability dashboard** (`/admin/observability`) — không bắt buộc Grafana/Kibana. |
| **T+02:00** (00:00) | **Hypercare Monitoring II** | Lead Dev | Đánh giá chỉ số hiệu năng (Latency p95 < 300ms, tỷ lệ lỗi CPU < 10%). | Gửi báo cáo vận hành đầu ca đêm cho FOUNDER. |

### 8.2 Quy trình hoãn Go-Live khẩn cấp (Rollback Plan)

Trường hợp cuộc họp quyết định **No-Go** tại mốc `T-00:30` hoặc hệ thống phát sinh lỗi nghiêm trọng sau go-live, DevOps thực hiện:
1. Chạy Script hạ cấp Database (Migration DOWN) để đưa cấu hình DB về trạng thái ổn định trước đó.
2. Trả quyền tiếp nhận PO/SO về cho Legacy WMS cũ.
3. Thông báo cho ERP SAP chuyển đổi endpoint nhận webhook trở lại hệ thống cũ.
4. Tắt service Local Agent trên các máy trạm nếu có xung đột thiết bị COM/USB.

### 8.3 Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

### 9.1 Go/No-Go Thresholds Checklist (Điều kiện quyết định Go-Live)

Trước mốc `T-00:30`, cuộc họp Go/No-Go giữa Lead Developer và FOUNDER sẽ rà soát các điều kiện sau để ký duyệt Go-live:

| STT | Chỉ số đánh giá | Ngưỡng đạt (Go Threshold) | Ngưỡng hoãn (No-Go Threshold) | Hành động khi No-Go |
|---|---|---|---|---|
| 1 | **Tỷ lệ kiểm thử UAT** | Pass 100% (4/4 kịch bản cốt lõi) | < 100% kịch bản UAT thành công | Hoãn go-live, rollback hệ thống về legacy, sửa bug khẩn cấp. |
| 2 | **Khôi phục dữ liệu (RTO)** | Diễn tập Restore DB thành công < 2 giờ | RTO diễn tập > 2 giờ hoặc restore lỗi | Dừng cutover, rà soát lại script backup/restore, tối ưu hóa kích thước dump. |
| 3 | **Code Signing Agent** | 100% máy trạm tin cậy file MSIX | Có máy trạm báo lỗi Windows SmartScreen chặn cài | IT support import thủ công Certificate vào Trusted Root CA hoặc dùng bản chạy portable. |
| 4 | **Độ trễ API Inbound** | p95 < 500ms khi test với SAP | p95 > 1000ms hoặc timeout liên tục | Hoãn go-live, kiểm tra cấu hình network, index DB và cấu hình memory Redis. |
| 5 | **Bảo mật Tenant** | Pass 100% test case Tenant Isolation | Có lỗi rò rỉ dữ liệu chéo giữa các tenant | **No-Go tuyệt đối**. Hủy cutover, vá lỗ hổng phân quyền ngay lập tức. |

### 9.2 Luật an toàn hệ thống (Hardening Guardrails)

* Bắt buộc vô hiệu hóa cổng HTTP (Port 80) trên máy chủ Production, chuyển hướng toàn bộ sang HTTPS (TLS 1.3).
* Chỉ mở cổng IP loopback `127.0.0.1:9000` cho Local Agent. Cấm tuyệt đối bind 0.0.0.0.
* Toàn bộ dữ liệu lịch sử cân và in của thủ kho phải thực hiện reconcile định kỳ 1 giờ một lần để đảm bảo không bị lệch số lượng.

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Incident Playbooks (Kịch bản ứng phó sự cố sản xuất)

Quy trình xử lý khẩn cấp khi gặp 3 kịch bản thảm họa hệ thống trong hoặc ngay sau khi cutover:

### 10.1 Kịch bản 1: Sập Database Production trong lúc cutover

* **Triệu chứng:** Kết nối DB báo `Connection Refused`, Web UI báo lỗi 500 diện rộng, API endpoint sập hoàn toàn.
* **Quy trình xử lý (Playbook):**
  1. **Xác định lỗi:** Chạy lệnh `docker ps` và `docker logs postgres-prod` để tìm nguyên nhân (Hết đĩa, lỗi RAM hoặc cấu hình sai tham số `max_connections`).
  2. **Giải phóng tài nguyên:** Nếu do tràn RAM/CPU, restart Docker service: `docker-compose restart postgres`.
  3. **Khôi phục khẩn cấp (Rollback DB):** Nếu DB bị hỏng vật lý dữ liệu (data corruption):
     - Xóa container DB cũ: `docker-compose down -v`.
     - Tạo lại container DB sạch: `docker-compose up -d postgres`.
     - Khôi phục từ bản backup `nexustock_prod_backup_T4.tar` mới nhất:
       ```bash
       pg_restore -U postgres -d nexustock_prod -v nexustock_prod_backup_T4.tar
       ```
  4. **Kiểm tra tính nhất quán:** Thực hiện query đối soát: `SELECT COUNT(*) FROM "InventoryBalances"`.
  5. **Báo cáo:** Gửi log và trace ID sự cố lên DevOps Admin.

### 10.2 Kịch bản 2: Local Agent sập diện rộng hoặc lỗi kết nối thiết bị

* **Triệu chứng:** Hàng loạt máy trạm của thủ kho báo lỗi đỏ "Không tìm thấy Local Agent" hoặc "Lỗi in tem hàng loạt".
* **Quy trình xử lý (Playbook):**
  1. **Cách ly thiết bị:** Hướng dẫn thủ kho chuyển sang chế độ **In tem thủ công** (Tải file PDF tem nhãn về máy tính local và in qua driver Windows truyền thống).
  2. **Kiểm tra trạng thái Service:** Trên máy trạm Windows, mở Command Prompt quyền Admin chạy:
     ```cmd
     sc query NexustockLocalAgent
     ```
     Nếu service báo `STOPPED`, chạy lệnh start: `net start NexustockLocalAgent`.
  3. **Kiểm tra chứng chỉ SSL:** Truy cập `https://127.0.0.1:9000/ping` trên trình duyệt máy trạm. Nếu báo lỗi "Your connection is not private", chạy lại script cài đặt cert tự ký trong thư mục cài đặt của Agent.
  4. **Quét cổng:** Nếu cổng `9000` bị chiếm, cấu hình lại file `appsettings.json` của Agent sang cổng `9001` và reload Web UI để tự quét cổng dự phòng.

### 10.3 Kịch bản 3: Mất kết nối liên thông với hệ thống ERP/SAP

* **Triệu chứng:** SAP báo lỗi không gửi được đơn PO/SO sang WMS (lỗi timeout). WMS không bắn được Goods Receipt Webhook sang SAP.
* **Quy trình xử lý (Playbook):**
  1. **Bật chế độ Offline Integration:** Kích hoạt chế độ tải đơn thủ công qua Excel (Import Wizard) trên Web UI WMS để thủ kho tiếp tục nhập/xuất hàng bình thường, không làm gián đoạn kho vật lý.
  2. **Kiểm tra Outbox Queue:** Trên WMS, kiểm tra bảng `IntegrationMessages` tìm các bản ghi có trạng thái `pending_retry` hoặc `failed`.
  3. **Ping Network:** Thực hiện ping và trace route cổng kết nối của SAP gateway từ server WMS.
  4. **Đồng bộ bù (Resync):** Khi đường truyền SAP phục hồi, DevOps chạy lệnh trigger gửi lại toàn bộ webhook lỗi trong hàng đợi:
     ```bash
     curl -X POST -H "Authorization: Bearer <token>" https://wms.nexustock.vn/api/admin/integration/retry-failed
     ```

### 10.4 Mapping lỗi chuẩn

| Nhóm lỗi | Hành vi hệ thống |
|---|---|
| Input sai | Trả validation error, không ghi transaction |
| Thiếu quyền | Trả 403, ghi security audit nếu cần |
| Dữ liệu stale | Trả conflict, yêu cầu reload |
| Vi phạm rule kho | Block hoặc tạo operational exception theo severity |
| Lỗi thiết bị/tích hợp | Ghi integration/device log, cho retry hoặc fallback nếu an toàn |
| Lỗi không khôi phục | Ghi trace ID, rollback transaction, báo admin |

### Nguyên tắc exception

* Lỗi vận hành có thể xử lý nghiệp vụ thì tạo exception framework.
* Lỗi kỹ thuật chỉ tạo operational exception nếu ảnh hưởng tác vụ kho.
* Không nuốt lỗi âm thầm.
* Mọi override phải có reason và audit.

## 11. Observability

* Go-live dashboard
* Incident log
* Alert watch

### Log và trace

* Mỗi request có trace ID.
* Command quan trọng ghi audit log.
* Entity nghiệp vụ chính ghi activity timeline.
* Job nền và integration event truyền trace ID khi liên quan flow gốc.
* Log không chứa password, token, secret hoặc dữ liệu nhạy cảm không mask.

### KPI đề xuất

* Throughput theo ngày/ca/user nếu phase có thao tác vận hành.
* Aging của task mở hoặc exception mở.
* Tỷ lệ lỗi validation/rule block.
* Tỷ lệ retry/failure nếu phase có tích hợp.
* Độ chính xác tồn kho nếu phase ảnh hưởng inventory.

## 12. Test plan

* Security
* Load
* E2E
* UAT
* Restore rehearsal

### Test matrix bắt buộc

| Nhóm test | Nội dung |
|---|---|
| Unit | Rule nghiệp vụ, status transition, validation helper |
| Integration | API + DB transaction + permission + concurrency |
| E2E | Luồng người dùng chính từ UI/RF/mobile |
| Negative | Sai quyền, sai trạng thái, dữ liệu stale, duplicate request |
| Regression | Không phá phase trước và dependency downstream |

### Dữ liệu test

* Tenant demo.
* User đủ quyền và user thiếu quyền.
* Master data hợp lệ và master data inactive.
* Bản ghi đang open/completed/cancelled để test transition.
* Dữ liệu conflict/concurrency nếu phase ghi transaction.

## 13. Acceptance criteria

Tất cả 14 criteria dưới đây phải đạt PASS trước khi FOUNDER ký go-live. Mỗi criteria phải có evidence (bằng chứng) cụ thể đính kèm.

| ID | Criteria | Evidence |
|---|---|---|
| AC-01 | Không có lỗi Critical hoặc High nào chưa được vá trong issue tracker | Screenshot issue board tại thời điểm go-live — **`up` 2026-07-21: FOUNDER = A** (sạch; vẫn chụp evidence khi go-live) |
| AC-02 | Rollback rehearsal hoàn tất thành công, RTO thực tế < 2 giờ | Video screen recording toàn bộ quy trình |
| AC-03 | Backup restore rehearsal hoàn tất, RPO thực tế < 1 giờ (timestamp diff DB) | Output SQL query: `MAX(createdAt)` vs thời điểm incident giả lập |
| AC-04 | UAT 4 scenario pass 100%: Inbound, QC, Pack+Scale, Print Error | Video walkthrough + biên bản UAT có chữ ký thủ kho |
| AC-05 | Load test 50 RF scanner đồng thời pass, p95 < 300ms | APM report hoặc k6/Locust output log |
| AC-06 | Allocation engine test 5,000 dòng hoàn thành < 1,000ms | Log file timestamp từ đầu đến cuối request |
| AC-07 | Security audit pass: tenant isolation, IDOR, Local Agent origin allowlist | Test report (manual hoặc tự động) theo test_strategy.md |
| AC-08 | ERP contract test 5 case pass: happy path, missing field, wrong type, duplicate key, oversized payload | xUnit test run log xanh toàn bộ — **`up` 2026-07-21: WAIVED** (SAP sandbox chưa sẵn); xem §19.13 + [WAIVER_AC08.md](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_30/WAIVER_AC08.md). Go-live ERP thật vẫn bắt buộc PASS sau này. |
| AC-09 | Observability dashboard hoạt động, hiển thị trace ID đầy đủ cho mọi request | Screenshot dashboard với trace ID sample |
| AC-10 | Feature flag bật/tắt hoạt động cho 5 phase core (P04, P06, P07, P13, P18) không cần redeploy | Manual test record: toggle flag → verify behavior |
| AC-11 | DB constraint pass: tồn kho không âm, idempotency key không duplicate | SQL query: `SELECT MIN(qty) FROM InventoryBalances` ≥ 0; duplicate key test |
| AC-12 | Code signing certificate Local Agent MSIX đã cài thành công trên máy trạm test | Output: `signtool verify /pa NexustockLocalAgent.msix` — No error |
| AC-13 | Cutover runbook có timeline từng giờ (T-4h đến T+2h), FOUNDER đã ký | Signed PDF document đính kèm |
| AC-14 | Không có hardcoded secret trong repo | `gitleaks detect --source .` — 0 findings |

### Definition of done

* Database migration chạy sạch trên database trống.
* API chính có test integration pass.
* UI/RF/mobile flow chính thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Exception path chính được test.
* README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* Không còn placeholder generic trong phần triển khai phase.



## 14. Out of scope

* Post go-live optimization

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

* Stage 1-3 + phase trước trong Stage 4

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.

## 16. Maintenance notes

* Automation phải explainable
* Luôn có manual override và reject reason
* Không để tối ưu phá rule an toàn

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

## 17. Extension points

* Tối ưu thuật toán
* Thêm ML/heuristic nâng cao
* Thêm integration thiết bị tự động

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

## 18. Rollback notes

* Tắt feature flag (nếu có flag cutover/readiness).
* Giữ transaction đã commit bằng corrective flow; không xóa log UAT/cutover/incident.

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.

---

## 19. rp1 Readiness audit (2026-07-21) — KHÔNG XÓA MỤC CŨ

> Audit `rp1`: kiểm tra phase đã đủ chuẩn 100% để execute chưa. Kết luận: **chưa Ready 100%**. Giữ nguyên section 1–18; phần này bổ sung gap phải khóa trước `/04-do-plan`.

### 19.1 Verdict

| Chỉ số | Giá trị |
|---|---|
| Maturity trước rp1 | 90% |
| Maturity sau rp1 (có gap list + contract skeleton) | **94%** |
| Maturity sau rp (khóa §19.10) | **96%** |
| Maturity sau `up` 2026-07-21 (AC-01=A + AC-08 waiver + evidence) | **100% Ready** |
| Ready execute 100%? | **✅ Có** (sau `up` 2026-07-21) — code vẫn ⬜ đến khi gọi execute |
| Blocker chính | DB/API/permission/UI contract chưa khóa chi tiết; thiếu implementation order + verify matrix + module boundary |

### 19.2 Gap chặn Ready 100% (phải khóa trước code)

| ID | Gap | Mức | Hành động bắt buộc |
|---|---|---|---|
| G01 | Bảng `UatRuns` / `CutoverLogs` / `IncidentDrills` thiếu cột/type/index | HIGH | Khóa schema đầy đủ (section 19.4) |
| G02 | API readiness/cutover thiếu request/response camelCase + error codes | HIGH | Khóa API matrix (section 19.5) |
| G03 | Chưa có permission seed / feature flag | HIGH | Seed tối thiểu (section 19.6) |
| G04 | UI routes/test IDs chưa chỉ định path file | MED | Khóa frontend checklist (section 19.7) |
| G05 | Thiếu implementation order + verify script | HIGH | Section 19.8–19.9 |
| G06 | Section 7 còn boilerplate RF/mobile generic không thuộc P30 | MED | Executor bỏ qua RF scan UX; chỉ làm 2 màn admin readiness/cutover |
| G07 | Observability mention Grafana/Kibana vs Phase 25 Next.js dashboard | MED | Dùng dashboard Observability hiện có (`/admin/observability*`); không bắt buộc Grafana mới trong P30 |
| G08 | AC-05 load 50 RF + AC-06 allocation 5k lines cần tool/script cụ thể | MED | Chỉ định k6 hoặc verify script; reuse `verify_allocation.ps1` cho AC-06 nếu đủ |
| G09 | Code signing MSIX (AC-12) phụ thuộc cert doanh nghiệp thật | HIGH | DoR: FOUNDER cung cấp cert hoặc chấp nhận mock/`signtool` skip có lý do trong rehearsal |
| G10 | SAP sandbox (điều kiện đầu vào) có thể không sẵn | HIGH | DoR gate: xác nhận sandbox trước execute hoặc đánh dấu AC-08 skip có lý do → **`up` 2026-07-21: WAIVED** ([WAIVER_AC08.md](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_30/WAIVER_AC08.md)) |

### 19.3 Upstream DoR (đã đạt sau Phase 29)

* Phase 01–29 trong [IMPLEMENTATION_PLAN.md](file:///d:/1_Project/48_Nexustock/planning/IMPLEMENTATION_PLAN.md) đã ✅ (tính đến 2026-07-21).
* Phase 26 đã có backup/restore/rollback scripts + Docker prod.
* Phase 25 đã có observability dashboard/trace.
* Incident playbook 3 kịch bản đã có ở section 10 (đáp ứng yêu cầu nâng 95% nêu ở header).

### 19.4 Database contract (bổ sung — khóa để execute)

Schema đề xuất: `readiness` (hoặc public nếu team chọn nhất quán module mới).

#### `uat_runs`

| Column | Type | Required | Ghi chú |
|---|---|:---:|---|
| `Id` | uuid | Yes | PK |
| `TenantId` | uuid | Yes | Tenant scope |
| `ScenarioCode` | varchar(64) | Yes | `INBOUND` / `QC` / `PACK_SCALE` / `PRINT_ERROR` |
| `Status` | varchar(32) | Yes | `Draft`, `Running`, `Passed`, `Failed`, `SignedOff` |
| `ResultNote` | varchar(1024) | No | Ghi chú UAT |
| `SignedOffBy` | varchar(128) | No | Actor ký |
| `SignedOffAt` | timestamptz | No | |
| `EvidenceUrl` | varchar(512) | No | Link video/biên bản |
| `TraceId` | varchar(128) | No | |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | audit | Yes/No | Chuẩn project |

Index: `(TenantId, ScenarioCode, CreatedAt DESC)`.

#### `cutover_logs`

| Column | Type | Required | Ghi chú |
|---|---|:---:|---|
| `Id` | uuid | Yes | PK |
| `TenantId` | uuid | Yes | |
| `StepCode` | varchar(64) | Yes | `FREEZE`, `BACKUP`, `DEPLOY`, `AGENT_ROLLOUT`, `SMOKE`, `GO_NO_GO`, `LIVE`, `HYPERCARE` |
| `Status` | varchar(32) | Yes | `Pending`, `Running`, `Done`, `Failed`, `Skipped` |
| `StartedAt` / `EndedAt` | timestamptz | No | |
| `Actor` | varchar(128) | Yes | |
| `Note` | varchar(1024) | No | |
| `TraceId` | varchar(128) | No | |

Index: `(TenantId, StepCode, StartedAt DESC)`.

#### `incident_drills`

| Column | Type | Required | Ghi chú |
|---|---|:---:|---|
| `Id` | uuid | Yes | PK |
| `TenantId` | uuid | Yes | |
| `ScenarioCode` | varchar(64) | Yes | `DB_DOWN`, `AGENT_DOWN`, `SAP_DOWN` |
| `RtoMinutes` | int | Yes | RTO thực tế |
| `Passed` | bool | Yes | |
| `ConductedBy` | varchar(128) | Yes | |
| `ConductedAt` | timestamptz | Yes | |
| `EvidenceNote` | varchar(1024) | No | |
| `TraceId` | varchar(128) | No | |

### 19.5 API contract (bổ sung)

Base: `/api/admin/readiness` và `/api/admin/cutover`  
Permission: xem section 19.6. Response camelCase.

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/api/admin/readiness` | `readiness.read` | Probe DB/Redis/SAP/LocalAgent summary |
| `GET` | `/api/admin/readiness/uat-runs` | `readiness.read` | List UAT runs |
| `POST` | `/api/admin/readiness/uat-runs` | `readiness.uat.write` | Tạo/cập nhật kết quả UAT |
| `POST` | `/api/admin/readiness/uat-runs/{id}/signoff` | `readiness.uat.signoff` | Ký UAT |
| `GET` | `/api/admin/cutover/logs` | `readiness.read` | Cutover board |
| `POST` | `/api/admin/cutover/freeze` | `readiness.cutover.freeze` | Bật read-only / freeze write |
| `POST` | `/api/admin/cutover/unfreeze` | `readiness.cutover.freeze` | Tắt freeze |
| `POST` | `/api/admin/readiness/incident-drills` | `readiness.drill.write` | Ghi kết quả diễn tập |

Error codes tối thiểu: `READINESS_UNAUTHORIZED`, `CUTOVER_FREEZE_DENIED`, `UAT_SIGN OFF_REQUIRED`, `READINESS_PROBE_FAILED`.

### 19.6 Feature flag & permissions (bổ sung)

| Flag | Default dev | Default prod | Mục đích |
|---|:---:|:---:|---|
| `FF_READINESS_GATE_ENABLED` | `true` | `true` | Bật API/UI readiness & cutover board |
| `FF_CUTOVER_FREEZE_ENABLED` | `true` | `false` đến giờ cutover | Cho phép freeze write |

| Permission | Scope |
|---|---|
| `readiness.read` | Xem dashboard/probe/logs |
| `readiness.uat.write` | Ghi UAT run |
| `readiness.uat.signoff` | Ký UAT (FOUNDER/admin) |
| `readiness.cutover.freeze` | Freeze/unfreeze |
| `readiness.drill.write` | Ghi incident drill |

### 19.7 Frontend checklist (bổ sung)

| File | Nội dung |
|---|---|
| `frontend/src/lib/readiness-api.ts` | Client typed |
| `frontend/src/app/admin/readiness/page.tsx` | System Readiness Dashboard |
| `frontend/src/app/admin/cutover/page.tsx` | Cutover Status Board (view + freeze nếu có quyền) |
| Sidebar | Label English: `Readiness`, `Cutover` |

Test IDs: `readiness-refresh-button`, `cutover-freeze-button`, `uat-signoff-button`.

**Lưu ý rp1:** Bỏ qua boilerplate RF/mobile ở section 7 cho Phase 30 — không tạo màn handheld mới.

### 19.8 Implementation order (bổ sung)

1. Tạo module `Nexustock.Modules.Readiness` (hoặc Observability extension — chọn 1, ưu tiên module riêng để không phình P25).
2. Entities + migration 3 bảng.
3. Seed flag + permissions.
4. Implement probe + freeze API.
5. UAT/cutover/drill APIs.
6. Admin UI readiness + cutover board.
7. Scripts: `tests/verify_readiness.ps1`, reuse backup/restore Phase 26 cho AC-02/03.
8. Chạy AC matrix 01–14 với evidence folder `planning/evidence/phase_30/`.
9. Cập nhật phase note + master plan sau signoff FOUNDER.

### 19.9 Verify matrix tối thiểu (bổ sung)

`tests/verify_readiness.ps1`:

1. Flag on + `readiness.read` → GET `/api/admin/readiness` 200.
2. Thiếu quyền → 403.
3. Freeze → ghi nghiệp vụ bị chặn (hoặc 423/409 theo contract).
4. Unfreeze → ghi lại được.
5. Tạo UAT run + signoff.
6. Ghi incident drill có `rtoMinutes`.
7. List cutover logs pagination.
8. Flag off → API disabled.

### 19.10 Open decisions — ĐÃ KHÓA (rp 2026-07-21)

> Reindex project: module pattern tách riêng (P25 Observability / P28 Labor / P29 TaskInterleaving); UI observability tại `/admin/observability*`; không có Grafana/Kibana trong repo; Local Agent MSIX + code signing thuộc ADR/P20–P22, rehearsal có thể defer như DoD dev các phase device.

| Câu hỏi | Quyết định khóa | Evidence / lý do |
|---|---|---|
| Module riêng hay nhét Observability? | **Module riêng `Nexustock.Modules.Readiness`** | Tránh phình P25; đồng bộ pattern module WMS độc lập + DI/migrate riêng. |
| Freeze toàn hệ thống hay chỉ inbound ERP? | **Freeze write API nghiệp vụ kho** (Inbound, Outbound, Inventory, QC, Wave, Allocation, …). Auth, readiness, cutover, observability **vẫn chạy**. ERP inbound write cũng bị chặn khi freeze. | Khớp cutover T-04:00 “WMS Read-Only”; vẫn probe/unfreeze được. |
| Grafana bắt buộc? | **Không** | Dùng Observability UI Phase 25 (`/admin/observability`, timeline, alerts). Sửa wording hypercare §8.1: theo dõi trace trên dashboard Observability (không bắt buộc Grafana/Kibana). |
| Cert signing thật? | **Rehearsal/DoD dev:** AC-12 được `SKIP` có lý do nếu chưa có enterprise cert. **Go-live thật:** AC-12 bắt buộc `signtool verify` pass. | Cùng tinh thần P22 (pilot thiết bị thật không bắt buộc DoD dev). |

**Override FOUNDER:** gửi khác quyết định trên trước khi execute; nếu không phản hồi = giữ khóa này.

### 19.11 Điều kiện nâng Ready 100% — ĐÃ ĐẠT (up 2026-07-21)

Phase 30 đạt **Ready 100% / được phép `/04-do-plan` hoặc `fp`/`tt`** sau khi FOUNDER khóa:

| # | Điều kiện | Kết quả |
|---|---|---|
| 1 | Open decisions §19.10 | ✅ Đã khóa `rp` 2026-07-21 |
| 2 | Schema 19.4 + API 19.5 + permission 19.6 | ✅ Đã khóa `rp1` (không TBD) |
| 3 | AC-01 Critical/High = 0 | ✅ **FOUNDER = A** (2026-07-21) — upstream issues sạch |
| 4 | SAP sandbox / AC-08 | ✅ **Waiver** — sandbox chưa sẵn; xem [WAIVER_AC08.md](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_30/WAIVER_AC08.md) |
| 5 | Thư mục evidence | ✅ `planning/evidence/phase_30/` đã tạo |

### 19.12 FOUNDER decision log (`up` 2026-07-21)

| ID | Quyết định | Chi tiết vận hành |
|---|---|---|
| AC-01 | **A** | Không còn Critical/High chưa vá. Evidence go-live vẫn chụp issue board sạch (AC-01 artifact). |
| AC-08 | **Waiver** (sandbox chưa sẵn) | Execute Phase 30 **không chặn** vì thiếu SAP. Probe SAP = Unavailable/Skipped. Smoke SAP cutover = Skipped. **Cấm** tuyên bố ERP production-ready cho đến khi chạy lại 5 case PASS và hủy waiver. |
| Evidence root | Đã tạo | [planning/evidence/phase_30](file:///d:/1_Project/48_Nexustock/planning/evidence/phase_30) + README checklist + WAIVER_AC08.md |

### 19.13 AC-08 vận hành dưới waiver (bổ sung — không xóa AC-08 gốc ở §13)

Giữ nguyên bảng AC §13. Khi execute / validate:

1. AC-08 status = `WAIVED` với link waiver.
2. Verify script không FAIL vì SAP down.
3. Readiness dashboard hiển thị SAP probe màu vàng/skip, không đỏ chặn cutover nội bộ kho.
4. Sau khi sandbox sẵn: chạy lại AC-08 → đổi status `PASS` + evidence file; cập nhật phase + master plan.

### 19.14 Execution gate sau Ready 100%

Được phép:

* `/17-auto-plan` / `fp` chi tiết execute module Readiness  
* `/04-do-plan` / `/18-auto-execute` / `tt` theo §19.4–19.9  

Thứ tự ưu tiên execute không đổi (§19.8). Out of scope RF handheld mới vẫn giữ.

**Trạng thái sau `up`:** Maturity **100% Ready**. Triển khai code vẫn ⬜ cho đến khi FOUNDER gọi execute.

---

