# Capacity Planning — Nexustock WMS

> **Scope:** Infrastructure sizing cho production deployment multi-warehouse
> **FOUNDER decision:** Multi-kho, LAN Agent scope
> **Baseline:** 50 RF scanners đồng thời / 1 kho, multi-warehouse per tenant

---

## 1. Concurrent User Estimate

| User type | Concurrent / warehouse | Note |
|---|---|---|
| RF Scanner (thủ kho cầm tay) | 20 - 50 | Mỗi scan = 1 API call < 300ms |
| Web UI (supervisor, QC) | 5 - 15 | Dashboard, report, master data |
| Local Agent (LAN) | 1 - 5 per warehouse | WebSocket persistent connection |
| ERP integration | 1 - 3 | Webhook inbound + outbound |
| Background jobs | 5 - 10 | Reservation TTL, replenishment, observability |

**Multi-warehouse scenario (v1.0 target):**
- Giả định: 3 kho đồng thời, mỗi kho 30 RF scanners
- Tổng concurrent API calls: ~100-150 requests/giây peak
- Tổng WebSocket connections: 10-20 (LAN Agent per warehouse)

---

## 2. Application Server Sizing

### 2.1 Minimum (1 kho, staging/test)

| Resource | Spec |
|---|---|
| CPU | 2 vCPU |
| RAM | 4 GB |
| Storage | 50 GB SSD |
| OS | Ubuntu 22.04 LTS |
| Runtime | .NET 8, Docker |

### 2.2 Recommended Production (multi-warehouse, 3 kho)

| Resource | Spec | Lý do |
|---|---|---|
| CPU | 4 vCPU | Rule Engine + Allocation algorithm CPU-bound |
| RAM | 8 GB | .NET runtime + EF Core query cache + in-memory allocation |
| Storage | 100 GB SSD | App logs, temp files, backup staging |
| Network | 100 Mbps | LAN Agent WebSocket + ERP webhook |
| OS | Ubuntu 22.04 LTS | Docker-optimized |

### 2.3 Scale trigger (khi nào cần nâng)

- CPU sustained > 70% trong giờ cao điểm → thêm 2 vCPU
- RAM usage > 80% → thêm 4 GB
- P95 API latency > 500ms cho query APIs → xem xét read replica hoặc thêm index

---

## 3. Database Server Sizing (PostgreSQL)

### 3.1 Spec đề xuất

| Resource | Minimum | Recommended |
|---|---|---|
| CPU | 2 vCPU | 4 vCPU |
| RAM | 4 GB | 8 GB |
| Storage | 200 GB SSD | 500 GB SSD (NVMe preferred) |
| IOPS | 1,000 | 3,000+ |

### 3.2 PostgreSQL Connection Pool

```ini
# postgresql.conf
max_connections = 200        # Tổng connection DB accept
shared_buffers = 2GB         # 25% RAM for DB cache
effective_cache_size = 6GB   # 75% RAM (query planner hint)
work_mem = 32MB              # Per-sort-operation memory
maintenance_work_mem = 256MB # VACUUM, CREATE INDEX
```

**Application-side pool (Npgsql):**

```json
{
  "ConnectionStrings": {
    "NexustockDb": "Host=db;Database=nexustock_main;Username=app;Password=xxx;
                    Minimum Pool Size=5;Maximum Pool Size=50;
                    Connection Idle Lifetime=300;Timeout=30"
  }
}
```

| Pool param | Value | Lý do |
|---|---|---|
| Minimum Pool Size | 5 | Warm pool, tránh cold start |
| Maximum Pool Size | 50 | Tổng 3 app instances x 50 = 150, dưới max_connections=200 |
| Idle Lifetime | 300s | Giải phóng connection không dùng sau 5 phút |
| Timeout | 30s | Fail fast nếu DB quá tải |

### 3.3 Storage Growth Estimate

| Data type | Growth / tháng | Retention | Total 5 năm |
|---|---|---|---|
| InventoryTransactions (ledger) | ~2 GB | 5 năm (bắt buộc) | ~120 GB |
| AuditLogs | ~1 GB | 5 năm (bắt buộc) | ~60 GB |
| AllocationReservations (active) | ~100 MB | 90 ngày rolling | ~300 MB |
| IntegrationOutbox | ~200 MB | 30 ngày rolling | ~200 MB |
| Master data (items, locations) | ~50 MB | Indefinite | ~50 MB |
| **Total** | **~3.3 GB/tháng** | — | **~200 GB** |

→ **500 GB SSD** cung cấp đủ buffer cho 5 năm + buffer 50% tăng trưởng.

---

## 4. Redis Cache Sizing (Optional nhưng recommended)

> Redis là optional theo NFR nhưng **strongly recommended** cho Rule Engine cache và Session cache khi scale multi-kho.

### 4.1 Spec

| Resource | Value |
|---|---|
| RAM | 512 MB (đủ cho MVP multi-kho) |
| Eviction policy | `allkeys-lru` |
| Persistence | RDB snapshot mỗi 5 phút (không cần AOF) |
| Max memory policy | `maxmemory-policy allkeys-lru` |

### 4.2 Cache key strategy

| Cache | Key pattern | TTL | Size estimate |
|---|---|---|---|
| Rule Engine compiled rules | `rules:{tenantId}:{warehouseId}` | 5 phút | ~1 MB/tenant |
| Item master data | `item:{tenantId}:{itemId}` | 10 phút | ~500 KB/tenant |
| User session (JWT blacklist) | `blacklist:jwt:{jti}` | = JWT expiry | ~100 bytes/token |
| Allocation lock check (advisory) | `alloc:lock:{shipmentId}` | 60s | ~50 bytes/shipment |

**Total Redis RAM estimate:** 3 tenants × ~2 MB/tenant = ~6 MB active data → 512 MB là rất dư cho scale hợp lý.

---

## 5. Local Agent (LAN scope) Sizing

> **FOUNDER decision:** Local Agent deploy trên LAN, không chỉ localhost — 1 Agent có thể phục vụ nhiều máy trạm thủ kho trong cùng subnet.

### 5.1 Network spec per warehouse

| Resource | Value |
|---|---|
| Agent bind address | `0.0.0.0:9000` (LAN) — không expose internet |
| Firewall rule | Allow inbound 9000/tcp từ warehouse subnet only |
| Concurrent WebSocket clients | Tối đa 20 (1 per máy trạm thủ kho) |
| Bandwidth | < 1 Mbps (thao tác scale + print là text/binary nhỏ) |

### 5.2 Agent server sizing

| Resource | Spec |
|---|---|
| CPU | 1 vCPU (hoặc dedicated mini PC) |
| RAM | 2 GB |
| OS | Windows Server 2019+ hoặc Windows 10/11 Pro |
| COM ports | 1-4 (cân điện tử) |
| USB/LAN | Zebra printer |

**LAN security note:** Vì Agent bind `0.0.0.0` thay vì `127.0.0.1`, phải có:
- Origin allowlist nghiêm ngặt (chỉ allow domain Web UI chính thức)
- HMAC timestamp validation (chống replay)
- Pairing token per station
- Firewall rule ở router kho: block 9000/tcp từ ngoài subnet

---

## 6. Network Bandwidth Estimate

| Traffic type | Peak bandwidth |
|---|---|
| RF Scanner API calls (50 scans/s) | ~5 Mbps |
| Web UI (3 warehouses, 15 supervisors) | ~10 Mbps |
| Local Agent WebSocket (60 connections) | ~2 Mbps |
| ERP webhook (burst 100 events/min) | ~1 Mbps |
| Backup upload (nightly, 3.3 GB) | ~10 Mbps (off-peak) |
| **Total peak** | **~18 Mbps** |

→ **100 Mbps** kết nối là đủ, với headroom 5x cho spike.

---

## 7. Backup & Storage

| Backup type | Frequency | Retention | Storage |
|---|---|---|---|
| PostgreSQL full dump | Daily 01:00 AM | 30 ngày | ~3.3 GB × 30 = ~100 GB |
| PostgreSQL WAL (incremental) | Continuous | 7 ngày | ~5 GB |
| App logs | Rolling | 30 ngày | ~10 GB |
| Agent config backup | On change | 5 versions | ~50 MB |

**Storage server:** Tách riêng backup storage khỏi DB server. Tối thiểu 200 GB NFS hoặc S3-compatible object storage.

---

## 8. Scale-up Decision Matrix

| Signal | Threshold | Action |
|---|---|---|
| API P95 latency > 500ms | Sustained 15 phút | Check DB query plan, add index |
| CPU > 70% app server | Sustained 30 phút | Scale vertical: +2 vCPU |
| DB connections > 150 | Sustained | Scale pool hoặc add read replica |
| Redis evictions > 0 | Per hour | Tăng Redis RAM lên 1 GB |
| RF Scan error rate > 1% | Per 10 phút | Check network, DB, Agent |
