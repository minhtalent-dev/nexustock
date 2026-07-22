# Rollback Rehearsal — Phase 37

**Ngày:** 2026-07-22  
**Môi trường:** Local pilot (API `:5024` · postgres qua compose)  
**Docker CLI trên host:** không có trong PATH → nhánh **PASS*** `RESTORE_SKIPPED_SAFE`

## Mục tiêu

RTO < 120 phút (P30 AC-02).

## Thực hiện

| Bước | Thời điểm | Kết quả |
|---|---|---|
| 1. Xác nhận API live | 2026-07-22 | PASS (`/health/live` 200) |
| 2. Backup qua `docker exec pg_dump` | — | **SKIP** — `docker` không có trên PATH shell execute |
| 3. Restore `ALLOW_RESTORE_TO_TARGET=true` | — | **SKIP** — tránh phá DB pilot đang verify |
| 4. Ước lượng RTO | — | **~15 phút** (dump+restore local postgres 16 volume nhỏ — ước lượng vận hành) |

## Verdict

| Field | Value |
|---|---|
| status | **PASS*** |
| code | `RESTORE_SKIPPED_SAFE` |
| rtoMinutes | **15** (ước lượng) |
| note | Backup/restore script P26 sẵn (`scripts/db-backup.sh`, `db-restore.sh`). Rehearsal phá DB hoãn đến khi FOUNDER cho phép trên DB phụ. |

**FOUNDER chấp nhận PASS*:** ☐ Có · ☐ Yêu cầu restore thật trước ký production
