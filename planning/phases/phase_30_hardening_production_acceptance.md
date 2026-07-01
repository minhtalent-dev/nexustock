# PHASE 30: Production Readiness Gate

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
- Không còn lỗi Critical hoặc High nào chưa được vá trong issue tracker.
- Môi trường SAP sandbox hoạt động ổn định và sẵn sàng cho việc test liên thông dữ liệu.

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
* Tách CSS/JS riêng nếu là web truyền thống; với SPA dùng component/style module nhất quán.
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

1. Freeze data
2. Backup
3. Migrate rehearsal
4. Run tests
5. UAT
6. Go/no-go
7. Cutover
8. Monitor

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Không go-live nếu critical fail
* Rollback đã diễn tập
* Data reconcile bắt buộc

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Data mismatch
* Performance fail
* Permission mismatch

### Mapping lỗi chuẩn

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

* Có signoff và rollback đã diễn tập

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

* Tắt feature flag
* Clear recommendation queue
* Giữ transaction đã commit bằng corrective flow

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.




