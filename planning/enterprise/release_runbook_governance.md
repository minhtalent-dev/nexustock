# Release runbook and governance (Sổ tay hướng dẫn phát hành và Cắt chuyển hệ thống)

Tài liệu hướng dẫn các bước chi tiết cho quy trình phát hành (Release) và cắt chuyển dữ liệu (Cutover) sang môi trường Production của dự án Nexustock WMS.

---

## 1. Bản kiểm tra Phát hành (Release Checklist)

Quy trình phát hành bắt buộc phải đi qua 6 bước kiểm soát độc lập để tránh gián đoạn kho:

### 1.1 Chuẩn bị & Build (Stage 1)
- [ ] Chạy test suite toàn bộ dự án tại local và CI: `dotnet test` và `npm run test` phải pass 100%.
- [ ] Build Docker image chính thức: `docker compose -f docker-compose.prod.yml build`.
- [ ] Xác nhận file cài đặt Local Agent MSIX đã được ký số bằng Certificate chính thức.

### 1.2 Sao lưu dữ liệu cũ (Stage 2 - Backup)
- [ ] Thực hiện lệnh khóa ghi tạm thời trên API: `POST /api/admin/cutover/freeze`.
- [ ] Chạy lệnh backup nóng database PostgreSQL:
  ```bash
  pg_dump -U postgres -h localhost -d nexustock_main -F c -b -v -f "/var/backups/nexustock/pre_release_$(date +%Y%m%d%H%M%S).backup"
  ```
- [ ] Xác nhận file backup đã lưu trữ thành công trên Cloud/Storage vật lý an toàn và dung lượng > 0.

### 1.3 Cập nhật Database (Stage 3 - Migration)
- [ ] Chạy script database migration trên môi trường production.
- [ ] Kiểm tra bảng `__MigrationHistory` để đảm bảo migration chạy thành công không có lỗi pending.

### 1.4 Chạy smoke test (Stage 4 - Smoke Test)
- [ ] Kiểm tra endpoint `/health/live` và `/health/ready` trả về HTTP 200.
- [ ] Kiểm tra kết nối WebSocket trạm Local Agent thành công.
- [ ] Mở API freeze lock để mở cổng giao dịch trở lại.

### 1.5 Nghiệm thu & Rollback (Stage 5)
- [ ] Nếu smoke test lỗi hoặc sập kết nối không thể tự vá dưới 30 phút, kích hoạt **Quy trình rollback khẩn cấp (Section 3)**.
- [ ] Nếu mọi thứ hoạt động tốt, xin phê duyệt signoff chính thức từ FOUNDER.

---

## 2. Biên bản kiểm duyệt Go/No-Go (Go/No-Go Template)

Trước giờ phát hành chính thức (thường là 22:00 ngày cuối tuần khi kho nghỉ giao dịch), Developer chính và FOUNDER phải họp duyệt:

```text
====================================================================
BIÊN BẢN DUYỆT PHÁT HÀNH NEXUSTOCK WMS
Thời gian họp: ...
Người chủ trì: FOUNDER
Người báo cáo kỹ thuật: Dev chính
====================================================================

CÁC TIÊU CHÍ GO/NO-GO:
1. Kết quả Test tự động (Đạt/Không đạt): ...
2. Kết quả UAT Signoff (Đạt/Không đạt): ...
3. Thời gian RTO khôi phục thử nghiệm (Đạt/Không đạt): ...
4. Trạng thái kết nối SAP sandbox (Sẵn sàng/Không sẵn sàng): ...
5. Chứng chỉ số Local Agent (Đã ký/Chưa ký): ...

QUYẾT ĐỊNH CUỐI CÙNG (GO hoặc NO-GO): [   ]
Ghi chú/Hành động bổ sung: ...
====================================================================
```

---

## 3. Quy trình Quay lui Khẩn cấp (Rollback Plan)

Trong trường hợp phát hành phiên bản mới gặp lỗi nghiêm trọng (sập DB, mất dữ liệu, Local Agent bị chặn Windows SmartScreen hàng loạt), Developer áp dụng quy trình rollback sau dưới 2 tiếng:

1. **Khóa cổng Web/API:** Tắt container frontend/backend để chặn người dùng kho thao tác.
2. **Khôi phục Database:**
   - Xóa database lỗi: `dropdb -U postgres nexustock_main` (sau khi đã sao lưu bản lỗi để đối soát sau).
   - Tạo lại database trống: `createdb -U postgres nexustock_main`.
   - Khôi phục từ file backup lưu tại Stage 2:
     ```bash
     pg_restore -U postgres -d nexustock_main -v "/var/backups/nexustock/pre_release_xxxx.backup"
     ```
3. **Quay lui phiên bản Code (Rollback Code):**
   - Đẩy (Deploy) Docker image của phiên bản ổn định trước đó (Rollback tag).
   - Rollback phiên bản Local Agent trên các máy trạm thủ kho nếu có thay đổi logic WebSocket.
4. **Mở kết nối & Smoke Test lại:** Chạy lại smoke test để đảm bảo kho hoạt động ổn định trên bản cũ.
5. **Thông báo sự cố:** Gửi thông báo đến Ops Lead và các bên liên quan theo mẫu:
   > *"Hệ thống Nexustock phát sinh lỗi tương thích thiết bị ngoại vi trong đợt cập nhật ngày... Chúng tôi đã thực hiện rollback thành công về phiên bản cũ lúc... Hoạt động của kho hiện bình thường."*

---

## 4. Ma trận hỗ trợ Hypercare (Hypercare Support & Escalation)

Giai đoạn Hypercare diễn ra trong 72 giờ đầu tiên sau go-live. Developer chính chịu trách nhiệm trực chiến:

| Cấp độ lỗi | Triệu chứng | RTO tối đa | Quy trình leo thang (Escalation) |
|---|---|---|---|
| **Lỗi Cấp 1 (Critical)** | Sập kho, mất dữ liệu, không in được tem, cân không hoạt động | 1 giờ | Dev chính xử lý ngay. Báo cáo trực tiếp FOUNDER mỗi 15 phút. |
| **Lỗi Cấp 2 (High)** | Một số RF scanner bị đơ kết nối, lỗi đồng bộ SAP lẻ | 4 giờ | Dev chính chẩn đoán, đưa phương án fix nóng hoặc manual override. |
| **Lỗi Cấp 3 (Medium)** | Sai lệch giao diện hiển thị, thiếu báo cáo KPI | 24 giờ | Ghi nhận issue tracker, lập kế hoạch fix vào sprint sau. |
