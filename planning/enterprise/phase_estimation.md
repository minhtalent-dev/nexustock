# Phase estimation (Ước lượng thời gian và nguồn lực triển khai)

Bảng ước lượng thời gian triển khai (Dev-Days) cho 30 phase của dự án Nexustock WMS.

## Nguyên tắc ước lượng (Estimation Principles)

- **Dev-Day:** Thời gian làm việc thực tế của 1 developer có trình độ Senior trong 1 ngày (8 tiếng).
- **Phạm vi bao gồm:** Code backend, viết unit/integration tests, code frontend/RF, cấu hình database và chạy thử local.
- **Complexity (Độ phức tạp):** 1 (Rất dễ) -> 5 (Rất khó - thuật toán, tích hợp phần cứng hoặc hệ thống ngoài).
- **Confidence (Mức độ tự tin):** High (Hiểu rõ nghiệp vụ & công nghệ), Medium (Cần làm rõ vài chi tiết), Low (Có rủi ro về thiết bị hoặc hệ thống bên thứ ba).

---

## Bảng ước lượng chi tiết

| Phase | Tên Phase | Complexity | Confidence | Dev-Days Range | Phân bổ nguồn lực (Role Mix) | Yếu tố gây tăng thời gian (Complexity Drivers) |
|---|---|---|---|---|---|---|
| **01** | Project foundation | 2 | High | 2 - 4 ngày | DevOps (40%), Backend (40%), QA (20%) | Thiết lập Docker compose, CI/CD mẫu và cấu trúc monorepo ban đầu. |
| **02** | Master data foundation | 2 | High | 3 - 5 ngày | Backend (50%), Frontend (30%), QA (20%) | Xử lý import Excel dữ liệu nền và hiển thị dạng cây (Tree) kho - khu vực. |
| **03** | User, RBAC & audit | 2 | High | 3 - 5 ngày | Backend (60%), Frontend (20%), QA (20%) | Middleware tự động ghi log thay đổi dữ liệu cũ/mới (old/new values) an toàn. |
| **04** | Inbound receiving | 3 | High | 4 - 6 ngày | Backend (50%), Frontend (30%), QA (20%) | Xử lý nhận hàng vượt dung sai (Tolerance) và logic tạo Lot tự động. |
| **05** | QC hold/release | 2 | High | 3 - 4 ngày | Backend (50%), Frontend (30%), QA (20%) | Đồng bộ trạng thái QC của Lot sang tồn kho khả dụng để chặn phân bổ. |
| **06** | Inventory location | 3 | High | 4 - 6 ngày | Backend (60%), Frontend (20%), QA (20%) | Thiết kế bảng Ledger bất biến, xử lý ghi đồng thời (concurrency) và chống âm kho. |
| **07** | Outbound picking | 3 | High | 5 - 7 ngày | Backend (50%), Frontend (30%), QA (20%) | Luồng nghiệp vụ dài từ Shipment -> Pick -> Pack -> Ship và cập nhật tồn kho. |
| **08** | Cycle count | 3 | High | 4 - 5 ngày | Backend (50%), Frontend (30%), QA (20%) | Cơ chế khóa vị trí (Location lock) tránh di chuyển hàng khi đang đếm. |
| **09** | RF/mobile core | 3 | High | 5 - 7 ngày | Mobile Dev (60%), Backend (20%), QA (20%) | Thiết kế giao diện tối giản cho handheld, tự động focus input, scan xử lý nhanh. |
| **10** | Exception framework | 2 | Medium | 3 - 4 ngày | Backend (60%), Frontend (20%), QA (20%) | Liên kết vết (Traceability) lỗi vận hành với các thực thể gốc (lot, location, task). |
| **11** | Rule engine foundation | 4 | Medium | 4 - 6 ngày | Backend (70%), Frontend (10%), QA (20%) | Thiết kế cấu trúc JSON điều kiện động và bộ lọc ưu tiên hoạt động tối ưu. |
| **12** | Putaway slotting | 3 | Medium | 4 - 6 ngày | Backend (60%), Frontend (20%), QA (20%) | Thuật toán gợi ý cất hàng theo sức chứa (Capacity), Zone và nhóm hàng phù hợp. |
| **13** | Allocation | 4 | Medium | 5 - 7 ngày | Backend (70%), Frontend (10%), QA (20%) | Chiến lược phân bổ FEFO/FIFO ngặt nghèo, lock dòng tồn kho để tránh trùng lặp. |
| **14** | Replenishment | 3 | Medium | 3 - 5 ngày | Backend (60%), Frontend (20%), QA (20%) | Job nền tự động tính toán tồn kho Pick Face để phát sinh nhiệm vụ bổ sung. |
| **15** | LPN pallet | 3 | Medium | 4 - 6 ngày | Backend (60%), Frontend (20%), QA (20%) | Di chuyển nguyên pallet (LPN) kéo theo cập nhật hàng loạt số lượng con bên trong. |
| **16** | Serial tracking | 3 | Medium | 4 - 5 ngày | Backend (60%), Frontend (20%), QA (20%) | Quản lý vòng đời serial (nhận, di chuyển, xuất) và validate số lượng khớp 1-1. |
| **17** | RMA return flow | 3 | High | 4 - 5 ngày | Backend (50%), Frontend (30%), QA (20%) | Tiếp nhận hàng trả về, bắt buộc phân loại QC trước khi cho phép cất lại vào kho. |
| **18** | Wave picking | 3 | Medium | 4 - 6 ngày | Backend (60%), Frontend (20%), QA (20%) | Thuật toán gom đơn hàng (Wave) theo khu vực lấy hàng để tối ưu quãng đường. |
| **19** | Material genealogy | 4 | Medium | 4 - 6 ngày | Backend (70%), Frontend (10%), QA (20%) | Truy vấn đệ quy cây Lot cha/con để hiển thị sơ đồ truy vết chất lượng sản phẩm. |
| **20** | Local Agent foundation | 4 | Low | 4 - 5 ngày | Agent Dev (50%), Backend (30%), QA (20%) | WebSocket bảo mật tự ký chứng chỉ, cơ chế bắt tay (Pairing code) với trình duyệt. |
| **21** | Scale integration | 4 | Low | 3 - 5 ngày | Agent Dev (60%), Backend (20%), QA (20%) | Đọc cổng COM vật lý, xử lý lọc nhiễu nhảy số cân, fallback ghi đè có audit. |
| **22** | Label printing | 4 | Medium | 4 - 5 ngày | Agent Dev (50%), Backend (30%), QA (20%) | Biên dịch mã Zebra ZPL/TSC TSPL, quản lý hàng đợi in, ghi nhận lý do in lại. |
| **23** | ERP legacy contract | 3 | Low | 5 - 7 ngày | Backend (60%), Integration (20%), QA (20%)| Khảo sát định dạng file/API của hệ thống ERP cũ, viết adapter mapping dữ liệu. |
| **24** | Integration reliability | 4 | High | 4 - 6 ngày | Backend (70%), DevOps (10%), QA (20%) | Xây dựng Outbox Worker, quản lý DLQ, ký HMAC bảo mật webhook. |
| **25** | Operational observability| 3 | High | 4 - 5 ngày | DevOps (40%), Backend (40%), QA (20%) | Cấu hình log tập trung OpenTelemetry, truyền Trace ID xuyên suốt các job nền. |
| **26** | DevOps deployment | 3 | High | 4 - 6 ngày | DevOps (70%), Backend (10%), QA (20%) | Viết script Docker Compose production, runbook backup và phương án rollback DB. |
| **27** | Cross-docking | 3 | Medium | 4 - 5 ngày | Backend (60%), Frontend (20%), QA (20%) | Thuật toán so khớp tức thời hàng vừa nhận tại cửa nhập với đơn hàng đang chờ xuất. |
| **28** | Labor tracking | 3 | High | 3 - 5 ngày | Backend (50%), Frontend (30%), QA (20%) | Ghi nhận thời gian bắt đầu/kết thúc tác vụ trên RF để tính hiệu suất lao động. |
| **29** | Task interleaving | 4 | Low | 4 - 6 ngày | Backend (70%), Mobile Dev (10%), QA (20%)| Thuật toán gợi ý tác vụ kép (ví dụ: cất hàng xong gợi ý lấy hàng gần đó). |
| **30** | Hardening & UAT | 3 | High | 6 - 8 ngày | Cả đội dự án (100%) | Chạy thử nghiệm tải lớn, diễn tập mất mạng, lỗi máy in, rollback và cutover. |

---

## Tổng kết dự báo dự án

- **Tổng thời gian phát triển ước lượng:** **118 - 164 ngày** (tương đương 5 - 7 tháng với team 3 dev).
- **Phân bổ nỗ lực trung bình:**
  - Phát triển Backend & DB: **60%**
  - Phát triển Frontend/RF/Mobile: **20%**
  - Tích hợp & Local Agent: **10%**
  - DevOps & Hardening: **10%**
- **Các phase rủi ro cao dễ trễ tiến độ:** Phase 13 (Allocation), Phase 20 (Local Agent), Phase 23 (ERP Integration). Cần ưu tiên làm rõ tài liệu kỹ thuật của các phase này trước khi code.
