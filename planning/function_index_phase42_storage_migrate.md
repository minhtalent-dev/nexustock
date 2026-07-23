# Function Index — Phase 42 Storage Provider Bulk Migrate

**Date:** 2026-07-23 · **Workflow:** `rp2` /17-auto-plan  
**SoT:** `planning/phases/phase_42_storage_provider_migrate.md`  
**Upstream:** Phase 41 **ĐÓNG** · Files module + `OpenReadAsync`  
**Maturity:** **100% Ready** (giữ sau `rp2`)

---

## 0. Bản đồ hệ thống (hiện trạng)

| Layer | Path / Artifact | Vai trò P42 |
|---|---|---|
| Module | `backend/modules/Nexustock.Modules.Files/` | **EXTEND** — job + worker + API migrate |
| Providers | `Providers/IObjectStorageProvider.cs` | **REUSE** OpenRead/Put/Exists/Delete — **MUST NOT** đổi contract |
| Settings | `FileStorageSettings` + `FileStorageSettingsService` | **REUSE** LastTestOk/At · ConfigJson · Resolve |
| Attachments | `file_attachments` | **UPDATE** provider + public_url sau copy |
| Admin UI | `frontend/src/app/admin/settings/storage/page.tsx` | **EXTEND** panel Migrate |
| Worker pattern | `WebhookOutboxWorker` | **MIRROR** BackgroundService + scoped DI |
| Permissions | `DatabaseSeeder` | **ADD** `files.storage.migrate.purge` |
| MUST NOT | Inbound/Outbound attach · P41 upload path · dual-write | → Phase **43** / giữ nguyên |

---

## 1. Function catalog (F01–F32)

### Domain / DB

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F01 | Entity `FileStorageMigrateJob` | EP0 | `Entities/FileStorageMigrateJob.cs` | eligible_ids jsonb · counts · status |
| F02 | Entity `FileStorageMigrateJobError` | EP0 | `Entities/FileStorageMigrateJobError.cs` | FK job cascade |
| F03 | EF config + schema `files` | EP0 | `FilesDbContext.cs` | Tenant filter trên Job |
| F04 | Migration `AddStorageMigrateJobs` | EP0 | `Migrations/*_AddStorageMigrateJobs.cs` | UP/DOWN §6 |
| F05 | Seed permission purge | EP2 | `DatabaseSeeder.cs` | `files.storage.migrate.purge` → Admin only |
| F06 | Config `Migrate:MaxParallel` | EP1 | `appsettings` + options | Default **1** |

### Services / Worker

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F07 | `IStorageMigrateService.DryRunAsync` | EP1 | `Services/StorageMigrateService.cs` | Count + sample 20 · no write |
| F08 | `StartJobAsync` | EP1 | same | Snapshot ≤2000 · Test gate 24h · 409 if RUNNING |
| F09 | `GetJobAsync` / list latest | EP1 | same | Poll FE |
| F10 | `CancelAsync` | EP2 | same | Flag; worker finish current item |
| F11 | `ResumeAsync` | EP2 | same | PAUSED / FAILED / CANCELLED partial |
| F12 | `PurgeSourceAsync` | EP2 | same | Permission purge · only success on target |
| F13 | `GetErrorsAsync` | EP2 | same | take=50 |
| F14 | Resolve source/target providers | EP1 | reuse `ObjectStorageResolver` | Source config missing → `MIGRATE_SOURCE_CONFIG_INVALID` |
| F15 | `MigrateOneAsync` copy+verify+update | EP1 | worker / service | Skip if already target+Exists |
| F16 | `StorageMigrateWorker` claim loop | EP1 | `Workers/StorageMigrateWorker.cs` | Atomic PENDING→RUNNING |
| F17 | Progress bump mỗi N=10 | EP1 | worker | success/skip/fail + cursor |
| F18 | Status `COMPLETED_WITH_ERRORS` | EP1 | worker | fail_count>0 hết snapshot |
| F19 | Audit events migrate | EP2 | optional IAudit | start/cancel/purge |

### API

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F20 | `FileStorageMigrateController` | EP1–EP2 | `Controllers/FileStorageMigrateController.cs` | camelCase DTOs |
| F21 | Authz manage / purge | EP1–EP2 | controller | manage=dry/start/cancel; purge=purge |
| F22 | Error codes §11 | EP1–EP2 | `FileDomainException` | + `MIGRATE_SOURCE_CONFIG_INVALID` |

### Frontend

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F23 | `storage-migrate-api.ts` | EP3 | `features/files/` | dry-run/jobs/poll |
| F24 | `StorageMigratePanel` | EP3 | `storage-migrate-panel.tsx` | source select · progress · purge confirm |
| F25 | Wire Admin Storage page | EP3 | `admin/settings/storage/page.tsx` | Section dưới Test/Save |
| F26 | i18n `Admin.storage.migrate.*` | EP3 | `messages/{en,vi}/Admin.json` | EN UI · VI catalog |
| F27 | Poll 2s khi RUNNING | EP3 | panel | stop when terminal |

### Verify / Evidence / MUST NOT

| ID | Function | EP | Primary files | Notes |
|---|---|---|---|---|
| F28 | `verify_storage_migrate.ps1` | EP4 | `tests/` | Rules §22.5 |
| F29 | Evidence `phase_42/` + dbm | EP4 | `planning/evidence/phase_42*` | |
| F30 | Regression `verify_files_spreadsheet` | EP4 | existing | MUST PASS |
| F31 | Plan row 42 DoD | EP4 | `IMPLEMENTATION_PLAN.md` | |
| F32 | **MUST NOT** Inbound/Outbound attach / dual-write / đổi OpenRead contract / P43 UI | ALL | — | Checklist executor |

---

## 2. Trace EP ↔ F

| EP | Goal | F-ids | Validation |
|---|---|---|---|
| EP0 | Entities + migration + DI stub | F01–F04 | `dotnet ef` / build |
| EP1 | Service + worker + dry-run/start/status | F06–F09, F14–F18, F20–F21 | Fake LOCAL→FAKE migrate 5 files |
| EP2 | Cancel/resume/purge + errors + perm | F05, F10–F13, F19, F22 | 409 RUNNING · purge 403 |
| EP3 | Admin panel + i18n + poll | F23–F27 | dbm Storage migrate smoke |
| EP4 | verify + docs + plan | F28–F31 | verify PASS · F32 checklist |

---

## 3. Luồng runtime (rút gọn)

```text
Admin DryRun → eligibleCount
Admin Start → INSERT job PENDING + eligible_ids[≤2000]
Worker Claim RUNNING (atomic)
FOR EACH id IN eligible_ids AFTER cursor:
  IF cancel → CANCELLED; break
  OpenRead(src) → Put(dst) → Exists(dst) → UPDATE attachment
  bump counts / cursor
COMPLETED | COMPLETED_WITH_ERRORS
Admin optional PurgeSource → DeleteAsync(src) for success rows
```

---

## 4. MUST NOT (executor)

1. Không sửa contract `IObjectStorageProvider` (trừ bugfix).  
2. Không implement Phase **43** entity attach.  
3. Không default `delete_source_after=true`.  
4. Không migrate khi target Test FAIL / stale >24h (trừ inline re-test).  
5. Không cross-tenant.  
6. Không đổi `storage_key` scheme.  
7. Không phá P41 upload LOCAL / Admin Storage existing fields.

---

## 5. Happy path khóa

**LOCAL → active cloud** (S3/Azure/GCS/R2) sau Test ≤24h.  
CI: **FAKE → FAKE** hoặc LOCAL path temp → Fake.

---

## 6. Verdict index

**PASS** — F01–F32 đủ cho `/18` EP0–EP4; đồng bộ SoT §22 `rp1`.

---

## 7. `rp3` locks (bổ sung — không đổi F-id)

| Lock | Áp dụng |
|---|---|
| Worker tenant | IgnoreQueryFilters + TenantId (F16) |
| cancel_requested | F01 entity · F10 Cancel |
| target == ActiveProvider | F08 Start |
| GET jobs/active | F09 · F24 hydrate |
| No auto-purge | F12 |
| Fake Dev-only | F08 · F14 |

**`rp3` PASS** — 20/20 BS · maturity 100% Ready giữ.