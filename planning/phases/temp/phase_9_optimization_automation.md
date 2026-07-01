# PHASE 9: CROSS-DOCKING, LABOR TRACKING & TASK INTERLEAVING

Phase này tối ưu năng suất vận hành bằng tự động hóa điều phối, giảm thao tác thừa và giảm quãng đường di chuyển.

---

## 1. Mục tiêu

* Đề xuất Cross-Docking khi hàng vừa nhận khớp đơn xuất đang chờ.
* Đo năng suất lao động theo tác vụ thực tế.
* Đan xen nhiệm vụ để giảm quãng đường xe nâng hoặc nhân viên đi rỗng.

---

## 2. Cross-Docking

* Kiểm tra đơn xuất mở khi hoàn tất nhận hàng.
* Nếu item, Lot policy, QC status và số lượng phù hợp, đề xuất chuyển thẳng ra staging/packing.
* Cho phép Manager cấu hình mức tự động: chỉ gợi ý, cần duyệt, hoặc tự tạo task.
* Nếu cross-dock thất bại, hàng quay lại putaway flow thường.

---

## 3. Labor Tracking

* Ghi nhận thời điểm bắt đầu/kết thúc task.
* Gắn task với user, device, location, reference document và trace ID.
* Tính thời gian xử lý theo flow: receiving, QC, putaway, picking, packing, stocktake.
* Dashboard hiển thị năng suất theo người, ca, zone và loại task.

---

## 4. Task Interleaving

* Sau khi hoàn tất putaway, hệ thống tìm task gần vị trí hiện tại.
* Ưu tiên task cùng zone, cùng hướng di chuyển hoặc cùng thiết bị.
* Không gán task vượt quyền, vượt kỹ năng hoặc đang bị khóa bởi user khác.
* Nếu không có task phù hợp, đưa người vận hành về queue chờ.

---

## 5. Dữ liệu cần có

| Bảng | Mục đích |
|---|---|
| `CrossDockCandidates` | Đề xuất cross-dock |
| `LaborTasks` | Tác vụ lao động |
| `TaskAssignments` | Gán task cho user/device |
| `TaskEvents` | Timeline task |
| `TravelHints` | Gợi ý quãng đường hoặc thứ tự task |

---

## 6. API cần có

| API | Mục đích |
|---|---|
| `POST /api/cross-dock/evaluate` | Tìm đề xuất cross-dock |
| `POST /api/cross-dock/{id}/accept` | Chấp nhận đề xuất |
| `POST /api/labor/tasks/{id}/start` | Bắt đầu task |
| `POST /api/labor/tasks/{id}/complete` | Hoàn tất task |
| `GET /api/tasks/next` | Lấy task tiếp theo tối ưu |

---

## 7. Tiêu chí hoàn tất

* Cross-Docking chỉ đề xuất khi không vi phạm QC, Lot policy và allocation.
* Labor Tracking đo được thời gian thực hiện tác vụ chính.
* Task Interleaving gán được task tiếp theo theo vị trí và quyền.
* Có KPI năng suất và thời gian chờ.
* ponytail: Route optimization ban đầu dùng heuristic theo zone/vị trí. Nâng cấp graph routing 3D khi số vị trí lớn hoặc có AGV/AMR.
