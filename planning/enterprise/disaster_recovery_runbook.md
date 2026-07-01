# Disaster Recovery Runbook — Nexustock WMS

> **Level:** L3 Disaster Recovery (catastrophic failures only)
> **L1/L2 operational issues** (device offline, webhook stuck, inventory mismatch, barcode error) → Xem [support_runbook.md](./support_runbook.md)
> **RTO target:** 2 giờ | **RPO target:** 1 giờ (từ NFR)

---

## Phân tầng Severity

| Level | Mô tả | Runbook |
|---|---|---|
| L1 | Device offline, barcode error, print fail | support_runbook.md §1 |
| L2 | Webhook stuck, DLQ full, ghost reservation | support_runbook.md §2-3 |
| L3 | **DB crash, server total failure, data corruption, mass outage** | **File này** |

---

## Escalation Matrix

| Severity | Detection | First responder | Escalate nếu > |
|---|---|---|---|
| L3 — Critical | Uptime monitor alert / Users cannot login | Dev chính | 15 phút → FOUNDER |
| L3 — High | DB slow queries > 30s / Backup job fail | Dev chính | 30 phút → FOUNDER |

---

## SCENARIO 1: Database Server Crash

**Detection signals:**
- Toàn bộ Web UI và RF login báo `500 Internal Server Error`
- Health endpoint `GET /health/ready` trả `503`
- Uptime Kuma / monitoring alert ping fail

**Severity:** Critical (toàn hệ thống ngừng hoạt động)

**Recovery steps:**

1. SSH vào DB server. Chạy: `docker ps | grep postgres`
   - Container đang chạy → kiểm tra disk full: `df -h`
   - Container bị stop → `docker-compose -f /opt/nexustock/docker-compose.prod.yml restart db`

2. Nếu container restart thành công → kiểm tra DB healthy:
   ```bash
   docker exec nexustock-db pg_isready -U postgres
   ```
   Nếu trả `accepting connections` → Done. Kiểm tra `GET /health/ready`.

3. Nếu disk full → dọn dọn WAL logs cũ:
   ```bash
   docker exec nexustock-db psql -U postgres -c "SELECT pg_walfile_name(pg_current_wal_lsn());"
   # Xóa WAL segments cũ hơn 7 ngày nếu có checkpoint xong
   ```

4. Nếu DB không start được (data corruption):
   - Dựng DB instance mới: `docker-compose up -d db`
   - Restore từ backup gần nhất:
   ```bash
   ls -lt /var/backups/nexustock/*.sql.gz | head -5
   gunzip -c nexustock_YYYYMMDD_010000.sql.gz | docker exec -i nexustock-db psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS nexustock_main; CREATE DATABASE nexustock_main;"
   gunzip -c nexustock_YYYYMMDD_010000.sql.gz | docker exec -i nexustock-db psql -U postgres -d nexustock_main
   ```

5. Chạy migrations bổ sung nếu app version mới hơn backup:
   ```bash
   docker exec nexustock-api dotnet Nexustock.Api.dll --migrate
   ```

6. Validate sau restore:
   ```sql
   SELECT COUNT(*) FROM "InventoryBalances";
   SELECT MAX("createdAt") FROM "InventoryTransactions";
   -- Xác nhận timestamp gần nhất trong RPO window (< 1 giờ mất)
   ```

7. Notify FOUNDER: thông báo RPO thực tế (thời gian từ backup cuối đến sự cố).

8. Liên hệ ERP team: yêu cầu replay tất cả events trong khoảng thời gian bị mất (idempotency-key đảm bảo không duplicate).

**Communication template:**
```
[WMS INCIDENT] Database Recovery — [timestamp]
Status: RESOLVED / IN PROGRESS
Impact: [X] phút downtime. Dữ liệu trong [Y] phút trước incident cần đồng bộ lại với ERP.
Action required: ERP team replay orders từ [time_A] đến [time_B].
Next update: [timestamp + 30 phút]
```

---

## SCENARIO 2: Application Server Total Failure

**Detection:** App server không respond, container crash loop, memory OOM.

**Recovery steps:**

1. Kiểm tra container logs: `docker logs nexustock-api --tail 200`

2. OOM kill → restart với memory limit:
   ```bash
   docker-compose restart api
   # Nếu vẫn OOM: giảm tải, kill background jobs không cần thiết
   ```

3. Nếu app crash loop sau restart → deploy previous image:
   ```bash
   docker tag nexustock-api:previous nexustock-api:rollback
   docker-compose up -d api
   ```
   Previous image được giữ theo [release_runbook_governance.md].

4. Validate: `curl -s https://wms.nexustock.io/health/live` → 200 OK

5. Smoke test: Login, xem danh sách tồn kho, kiểm tra 1 RF scan.

**RTO checkpoint:** Nếu không recover trong 60 phút → deploy trên server dự phòng (nếu có) hoặc thông báo downtime extended cho FOUNDER.

---

## SCENARIO 3: Network Complete Outage (ISP Down)

**Detection:** Local Agent mất kết nối tới cloud API. RF Scanner không scan được.

**Impact:** Kho không thể thao tác WMS. Không mất dữ liệu đã commit.

**Recovery steps:**

1. Activate 4G backup router (theo R-08 trong risk_register.md):
   - Kiểm tra router 4G có auto-failover không: cài đặt tại router kho
   - Nếu không auto: cắm 4G USB modem, share connection thủ công

2. Nếu không có 4G backup: chuyển sang **Offline Paper Process**:
   - In bảng phiếu nhận hàng thủ công (template tại `/docs/offline_forms/`)
   - Thủ kho ghi chép tay: item code, lot, qty, location
   - Lưu trữ giấy cho đến khi có mạng

3. Khi mạng khôi phục:
   - Nhập bù dữ liệu offline vào WMS theo thứ tự thời gian
   - Dùng reason code `REASON-OFFLINE-BACKFILL` khi nhập
   - Tạo phiếu kiểm kê đột xuất để đối soát sau khi nhập bù

**Communication template:**
```
[WMS INCIDENT] Network Outage — [timestamp]
Status: IN PROGRESS
Impact: [Tên kho] không thể dùng WMS. Kho đang vận hành paper process.
ETA: [ước tính thời gian khắc phục từ ISP]
Action: Sau khi có mạng, dev chính sẽ hỗ trợ nhập bù dữ liệu.
```

---

## SCENARIO 4: Mass Local Agent Failure (Windows Update / Reboot)

**Detection:** Nhiều máy trạm trong kho cùng lúc mất kết nối Agent (WebSocket disconnect hàng loạt).

**Root cause thường gặp:** Windows Update forced restart trong giờ làm việc.

**Recovery steps:**

1. Kiểm tra Services.msc trên 1 máy trạm: `Nexustock Local Agent` — Stopped → Start
   - Nếu service config `Startup Type = Automatic (Delayed)` → sẽ tự start sau reboot
   - Nếu chưa config: `sc config NexustockLocalAgent start= delayed-auto`

2. Trên Web UI: màn hình Station Management sẽ hiện trạng thái `offline` cho các station bị disconnect.

3. Sau khi Agent restart: Agent tự reconnect WebSocket trong vòng 30 giây (auto-reconnect theo NFR).

4. Verify: Web UI heartbeat badge hiện `connected` trong 10 giây.

5. **Preventive:** Cấu hình Windows Update defer trên máy trạm kho:
   - Policy: Defer feature updates 1 year, defer quality updates 1 week
   - Schedule reboot vào 02:00 AM ngày Chủ Nhật (ngoài giờ làm việc kho)

---

## SCENARIO 5: PostgreSQL Data Corruption (File-level)

**Detection:** PostgreSQL log lỗi `invalid page in block`, `checksum failure`, `relation file corrupted`.

**Severity:** Critical — dữ liệu vật lý bị hỏng.

**Recovery steps:**

1. **DỪNG ngay app server** để tránh ghi thêm vào DB hỏng:
   ```bash
   docker-compose stop api
   ```

2. Backup toàn bộ DB directory hiện tại (ngay cả khi corrupt, để forensics):
   ```bash
   cp -r /var/lib/docker/volumes/nexustock_pgdata/_data /tmp/nexustock_corrupt_$(date +%Y%m%d_%H%M%S)
   ```

3. Thử repair bằng `pg_resetwal` (chỉ nếu corruption ở WAL, không phải data):
   ```bash
   docker exec nexustock-db pg_resetwal -f /var/lib/postgresql/data
   # CẢNH BÁO: Lệnh này có thể gây mất transaction chưa commit
   ```

4. Nếu repair thất bại → restore từ backup:
   - Restore theo SCENARIO 1 bước 4-6

5. Sau restore: chạy integrity check:
   ```sql
   SELECT schemaname, tablename
   FROM pg_tables
   WHERE schemaname = 'public';
   -- Thử SELECT COUNT(*) trên từng bảng quan trọng
   ```

6. **Post-incident:** Bật PostgreSQL checksum nếu chưa có (phát hiện sớm corruption lần sau):
   ```bash
   pg_checksums --enable -D /var/lib/postgresql/data
   ```

---

## RTO/RPO Tracking

| Scenario | RTO target | RPO target | Actual RTO | Actual RPO | Notes |
|---|---|---|---|---|---|
| DB crash (restart) | 30 phút | 0 | | | |
| DB crash (restore) | 2 giờ | 1 giờ | | | Fill sau mỗi incident |
| App crash | 30 phút | 0 | | | |
| Network outage | N/A (paper process) | 0 | | | |
| Mass Agent failure | 5 phút | 0 | | | |
| Data corruption | 2 giờ | 1 giờ | | | |

---

## Post-incident Review Checklist (5W1H)

Sau mỗi L3 incident, bắt buộc hoàn thành trong 48 giờ:

- [ ] **What happened?** Mô tả kỹ thuật chính xác của sự cố
- [ ] **When did it start?** Timeline từ detection đến resolution
- [ ] **Who was impacted?** Kho nào, bao nhiêu user, bao nhiêu transaction bị ảnh hưởng
- [ ] **Why did it happen?** Root cause analysis (không blame người, chỉ phân tích hệ thống)
- [ ] **How was it fixed?** Các bước recovery đã thực hiện
- [ ] **How to prevent?** Action items cụ thể: code fix, config change, monitoring rule mới

**Owner:** Dev chính (viết report) + FOUNDER (review và sign off).
