# OBJECTIVE

Áp dụng hết Phase A→G để đóng 7 residual gap `rp5` của Phase 29 Task Interleaving, đạt DoD 100% thật theo [phase_29_task_interleaving.md](file:///d:/1_Project/48_Nexustock/planning/phases/phase_29_task_interleaving.md).

**Không giữ fake ✅.** Docs Phase 29 đang `🔄`; chỉ tick ✅ sau gate G.1 xanh.

**Nguồn critic:** [critic_report.md](file:///C:/Users/mes/.gemini/antigravity/brain/17cf2960-4583-44a5-918a-5eb1c709dc96/critic_report.md) — đã reconcile toàn bộ CRITICAL/HIGH.

---

# USER REVIEW REQUIRED

> [!IMPORTANT]
> 1. **Migration unique Open** sẽ FAIL nếu DB còn nhiều `Status='Open'` cùng `(TenantId, UserId)`. Executor bắt buộc chạy cleanup SQL (Task A.1) và xác nhận query trùng = 0 trước migrate.
> 2. **Scoring align v1** có thể đổi thứ tự candidate so với bản hiện tại (bỏ Manhattan same-zone, đổi continuity +4). Chấp nhận vì đúng contract plan.
> 3. **Verify** cần `$ConnectionString` (hoặc docker/psql như cross-docking) để seed `MobileTasks` + expire recommendation. Không ConnectionString → scenario seedable = SKIP (không PASS giả). Gate “PASS ≥ 12” chỉ áp dụng khi có DB seed.
> 4. **AcceptAsync** hiện Commit rồi throw trong catch Rollback — executor phải sửa (Task A.4) trước khi tin scenario expired/conflict.
> 5. **http-error.ts** hiện không đọc `errorCode` — Phase F bắt buộc mở rộng parse payload để tách feature-disabled vs unauthorized.

---

# OPEN QUESTIONS

> [!NOTE]
> Không còn câu hỏi chặn execute. Mọi ambiguity đã khóa:

| Ambiguity | Quyết định an toàn |
|---|---|
| Unique Open + chưa expire? | Unique `(TenantId, UserId) WHERE "Status" = 'Open'` (PascalCase). Expire/supersede trong service trước insert. Không dùng `NOW()` trong filter. |
| API expire riêng? | **Không.** Verify SQL `UPDATE ExpiresAt`. |
| Scorer visibility? | `public static class TaskInterleavingScorer`. |
| Continuity +4? | Có `activeSession` → +4 (shift/session context), không dùng same location. |
| Same-zone distance? | Flat **35** (bỏ Manhattan gradient). |
| Solution entry? | `dotnet sln add` unit test project. |
| Mobile label? | English `Next task` theo phase contract. |
| SelectedScore precision? | Cùng migration A.2: `HasPrecision(18,4)`. |

---

# ARCHITECTURE OVERVIEW

- **Current Architecture:** Module `Nexustock.Modules.TaskInterleaving` scaffold đủ API/DB/UI; scoring inline lệch plan; không unique Open; không structured logs; không unit test; verify 5 PASS / 10 SKIP; admin thiếu state; mobile hub thiếu nav.
- **Target Architecture:** Cùng module boundary. Thêm: partial unique Open, supersede-before-insert, pure scorer v1, ILogger 6 events, unit test project trong solution, verify seed SQL, admin state machine, mobile Next task, Accept commit-safe exception path, http-error errorCode parse.
- **Constraints:** net8.0; EF Core Npgsql; schema `task_interleaving`; bảng nguồn `MobileTasks` (Inventory); corporate proxy/EDR — tránh NuGet mới nếu package đã có trong solution (xunit versions align MasterData.IntegrationTests); không đụng Allocation/Wave/ledger.

### Function index (as-is) — Phase 0

```text
GET /api/task-interleaving/next
  → FF_TASK_INTERLEAVING_ENABLED → task_interleaving.read
  → LaborSession Running
  → MobileTasks Open + AssignedUser null
  → Score INLINE (lệch plan)
  → INSERT recommendation (+candidates)  // KHÔNG supersede Open cũ
  → 200 Open | NoCandidate

POST .../accept
  → shared TX Inventory+TaskInterleaving
  → idempotency → assign MobileTask In_Progress
  → Commit rồi throw Expired/Conflict → catch Rollback (RISK)

POST .../reject → reason allowlist → Rejected idempotent

UI: /admin/task-interleaving | /mobile/tasks/next (không có menu hub)
```

### Files MUST change

| Path | Việc |
|---|---|
| `backend/modules/.../Contexts/TaskInterleavingDbContext.cs` | Unique Open + SelectedScore precision |
| `backend/modules/.../Migrations/*_AddOpenRecommendationUniqueIndex*.cs` | Migration mới |
| `backend/modules/.../Services/TaskInterleavingScorer.cs` | **NEW** |
| `backend/modules/.../Services/TaskInterleavingService.cs` | Supersede, scorer, logs, Accept catch |
| `tests/Nexustock.TaskInterleaving.UnitTests/*` | **NEW** + add to `Nexustock.sln` |
| `tests/verify_task_interleaving.ps1` | ConnectionString seed/expire/teardown |
| `frontend/src/lib/http-error.ts` | Parse errorCode + status |
| `frontend/src/app/admin/task-interleaving/page.tsx` | States |
| `frontend/src/app/mobile/page.tsx` | Next task menu |
| `frontend/src/app/mobile/tasks/next/page.tsx` | Sentence case copy |
| `planning/phases/phase_29_task_interleaving.md` | Evidence sau G |
| `planning/IMPLEMENTATION_PLAN.md` | Sync ✅ sau G |

### Files MUST NOT change

- Permission names / feature flag name  
- Public API routes & DTO camelCase field names  
- Inventory ledger, Allocation, Wave, Replenishment business rules  
- Các phase docs khác ngoài 29  

---

# EXECUTION PHASES

## Phase A - Database unique Open + GetNext supersede + Accept catch
- **Goal:** Tối đa 1 Open / (tenant,user); `/next` không 23505; Accept expired/conflict không che lỗi bằng Rollback sau Commit.
- **Risk Level:** HIGH
- **Dependencies:** Không
- **Expected System State:** Migration applied; 2× GET /next ổn định

### Task A.1 - Cleanup data trước migrate
- **Purpose:** Tránh unique index fail (Critic C1).
- **Primary Target Files:** SQL one-shot (không commit secret); chạy qua `psql`/`docker exec` local.
- **Files That MUST NOT Change:** Production data policy — chỉ local/dev.
- **Steps:**
  1. Chạy cleanup:
     ```sql
     -- Expire Open đã quá hạn
     UPDATE task_interleaving.task_recommendations
     SET "Status" = 'Expired', "ReasonCode" = 'TASK_EXPIRED', "UpdatedAt" = now()
     WHERE "Status" = 'Open' AND "ExpiresAt" < now();

     -- Supersede Open trùng, giữ CreatedAt mới nhất
     WITH ranked AS (
       SELECT "Id",
              ROW_NUMBER() OVER (PARTITION BY "TenantId", "UserId" ORDER BY "CreatedAt" DESC) AS rn
       FROM task_interleaving.task_recommendations
       WHERE "Status" = 'Open'
     )
     UPDATE task_interleaving.task_recommendations t
     SET "Status" = 'Superseded', "UpdatedAt" = now()
     FROM ranked r
     WHERE t."Id" = r."Id" AND r.rn > 1;
     ```
  2. Verify:
     ```sql
     SELECT "TenantId", "UserId", COUNT(*)
     FROM task_interleaving.task_recommendations
     WHERE "Status" = 'Open'
     GROUP BY 1, 2
     HAVING COUNT(*) > 1;
     ```
     Phải **0 row**.
- **Expected Output:** Không còn Open trùng.
- **Validation:** Query trên = empty.
- **Failure Recovery:** Không chạy A.2 nếu còn row.
- **Continuation Criteria:** Empty duplicate set.

### Task A.2 - Fluent + Migration partial unique + SelectedScore precision
- **Purpose:** Enforce DB + harden precision (Critic H3, M3).
- **Primary Target Files:**
  - [TaskInterleavingDbContext.cs](file:///d:/1_Project/48_Nexustock/backend/modules/Nexustock.Modules.TaskInterleaving/Contexts/TaskInterleavingDbContext.cs)
  - `backend/modules/Nexustock.Modules.TaskInterleaving/Migrations/` (file mới)
- **Steps:**
  1. Trong `TaskRecommendation` fluent:
     ```csharp
     entity.Property(e => e.SelectedScore).HasPrecision(18, 4);
     entity.HasIndex(e => new { e.TenantId, e.UserId })
       .HasDatabaseName("uq_recommendations_tenant_user_open")
       .IsUnique()
       .HasFilter("\"Status\" = 'Open'");
     ```
  2. Tạo migration từ thư mục Api/module đúng `--context TaskInterleavingDbContext`.
  3. `dotnet ef database update` (local).
- **Expected Output:** Index `uq_recommendations_tenant_user_open` tồn tại.
- **Validation:** `pg_indexes` / `\d task_interleaving.task_recommendations`.
- **Failure Recovery:** `migrations remove` nếu chưa apply; Down nếu đã apply.
- **Continuation Criteria:** Update OK.

### Task A.3 - Supersede/expire trước insert trong GetNext
- **Purpose:** Tránh 23505 (Critic C2).
- **Primary Target Files:**
  - [TaskInterleavingService.cs](file:///d:/1_Project/48_Nexustock/backend/modules/Nexustock.Modules.TaskInterleaving/Services/TaskInterleavingService.cs)
- **Steps:**
  1. Thêm private `SupersedeOpenRecommendationsAsync(tenantId, userId, actor, ct)`.
  2. Đầu `GetNextAsync` và trước insert trong `CreateNoCandidateResponse`:
     - Open + `ExpiresAt < UtcNow` → `Expired` + `TASK_EXPIRED` + log expired.
     - Open còn hiệu lực → `Superseded` + `UpdatedBy/At`.
  3. Prefer **một transaction ngắn** cho supersede + insert recommendation (+candidates).
- **Expected Output:** Luôn ≤1 Open / user.
- **Validation:** Gọi `/next` 2 lần → DB chỉ 1 Open; lần 1 row chuyển Superseded/Expired.
- **Failure Recovery:** Revert service; giữ migration.
- **Continuation Criteria:** Không 500 unique.

### Task A.4 - AcceptAsync commit-safe exception path
- **Purpose:** Sửa Commit-then-Rollback (Critic C3).
- **Primary Target Files:** `TaskInterleavingService.cs` (`AcceptAsync`)
- **Steps:**
  1. Dùng flag `committed` hoặc tách: business exceptions throw **sau** khi thoát khối `catch Rollback`.
  2. Pattern an toàn:
     - Cập nhật Expired/Superseded → `SaveChanges` → `Commit` → `throw` **ngoài** `catch` Rollback.
     - Hoặc `catch` chỉ Rollback nếu `!committed`.
  3. Không đổi error code public.
- **Expected Output:** 409 expired/conflict sạch, không secondary rollback error.
- **Validation:** Scenario 9–10 verify.
- **Failure Recovery:** Revert chỉ Task A.4.
- **Continuation Criteria:** Throw message đúng `TASK_RECOMMENDATION_EXPIRED` / `TASK_ALREADY_ASSIGNED`.

---

## Phase B - Align scoring + penalty (section 7)
- **Goal:** Formula deterministic đúng plan; explainable.
- **Risk Level:** MEDIUM
- **Dependencies:** A.3 khuyến nghị xong trước
- **Expected System State:** Scorer public + service wire

### Task B.1 - Tạo TaskInterleavingScorer
- **Purpose:** Pure function testable (Critic H2).
- **Primary Target Files:**
  - `d:\1_Project\48_Nexustock\backend\modules\Nexustock.Modules.TaskInterleaving\Services\TaskInterleavingScorer.cs` (**NEW**)
- **Files That MUST NOT Change:** Controller, public DTO shapes
- **Steps:**
  1. `public static class TaskInterleavingScorer` với input record/DTO nội bộ (location ids/zones, ageSeconds, step, hasSession, sameOperation, sameZone, missingLocation, isConflictRisk, isStale).
  2. Rules bắt buộc:
     - distance: same loc 45; same zone 35; diff zone 10; missing coords 20
     - age: `min(20, ageMinutes/3)` với `ageMinutes = ageSeconds/60m`
     - priority: HIGH 20 / MEDIUM 10 / LOW 5 / else 0
     - continuity: same op +8; hasSession +4; same zone +3
     - penalty: stale (>4h) +20; missing location +5; conflict risk +50
  3. Method `OrderCandidates(...)` tie-break: TotalScore DESC → PriorityScore DESC → AgeSeconds DESC → TaskId ASC.
- **Expected Output:** File compile.
- **Validation:** Unit tests Phase D.
- **Failure Recovery:** Xóa file scorer.
- **Continuation Criteria:** Build module OK.

### Task B.2 - Wire scorer vào TaskInterleavingService
- **Purpose:** Bỏ scoring inline lệch.
- **Primary Target Files:** `TaskInterleavingService.cs`
- **Steps:**
  1. Thay vòng foreach bằng gọi scorer.
  2. Map Explanation jsonb camelCase giữ `TaskScoreExplanationDto`.
  3. Không đổi response contract.
- **Expected Output:** `/next` explanation khớp bảng.
- **Validation:** Manual 2 task same/diff zone → 35 vs 10.
- **Failure Recovery:** Revert wire; giữ scorer.
- **Continuation Criteria:** Explanation đúng.

---

## Phase C - Structured observability logs
- **Goal:** 6 event section 12.
- **Risk Level:** LOW
- **Dependencies:** B
- **Expected System State:** ILogger inject

### Task C.1 - Inject ILogger + 6 log points
- **Primary Target Files:** `TaskInterleavingService.cs` (ctor + DI tự resolve)
- **Steps:**
  1. Inject `ILogger<TaskInterleavingService>`.
  2. Log Information với message chứa event name cố định:
     - `task_interleaving.recommendation.created`
     - `task_interleaving.recommendation.no_candidate`
     - `task_interleaving.recommendation.accepted`
     - `task_interleaving.recommendation.rejected`
     - `task_interleaving.recommendation.expired`
     - `task_interleaving.recommendation.conflict`
  3. Properties: recommendationId, tenantId, userId, traceId, status/reasonCode. Không log secret.
- **Expected Output:** Console/Serilog thấy event khi chạy flow.
- **Validation:** next → created/no_candidate; accept → accepted; reject → rejected; expire path → expired; conflict → conflict.
- **Failure Recovery:** Gỡ logger calls.
- **Continuation Criteria:** ≥ created + accepted + rejected khi verify.

---

## Phase D - Unit tests
- **Goal:** Scoring/tie-breaker/validation covered; project trong solution (Critic H1).
- **Risk Level:** LOW
- **Dependencies:** B.1
- **Expected System State:** `dotnet test` xanh

### Task D.1 - Tạo project + add solution + tests
- **Primary Target Files:**
  - `d:\1_Project\48_Nexustock\tests\Nexustock.TaskInterleaving.UnitTests\Nexustock.TaskInterleaving.UnitTests.csproj`
  - `TaskInterleavingScorerTests.cs`
  - `TaskInterleavingValidationTests.cs` (allowlist reason constants / helper nếu extract)
  - [Nexustock.sln](file:///d:/1_Project/48_Nexustock/Nexustock.sln)
- **Steps:**
  1. csproj net8.0; xunit packages **cùng version** MasterData.IntegrationTests (17.11.1 / 2.9.2 / 2.8.2).
  2. ProjectReference → `Nexustock.Modules.TaskInterleaving.csproj` only (không reference Api trừ khi cần).
  3. `dotnet sln Nexustock.sln add ...`
  4. Tests tối thiểu:
     - distance 45/35/10/20
     - age 30min → 10; ≥60min → 20 cap
     - priority HIGH>MEDIUM>LOW
     - continuity 8+4+3
     - penalty stale+missingLocation
     - tie-breaker TaskId ASC khi score/priority/age bằng
  5. `dotnet test tests/Nexustock.TaskInterleaving.UnitTests`
- **Expected Output:** 100% pass.
- **Validation:** Exit code 0.
- **Failure Recovery:** Sửa assertion theo plan v1 — **không** nới formula.
- **Continuation Criteria:** 0 fail.

---

## Phase E - Verify script seed → giảm SKIP
- **Goal:** FAIL=0; với ConnectionString: PASS≥12; scenario 4–5,7–12 không SKIP (Critic C4, H4).
- **Risk Level:** MEDIUM
- **Dependencies:** A–C
- **Expected System State:** Script idempotent 2 lần chạy

### Task E.1 - Param ConnectionString + seed MobileTasks
- **Primary Target Files:** [verify_task_interleaving.ps1](file:///d:/1_Project/48_Nexustock/tests/verify_task_interleaving.ps1)
- **Steps:**
  1. Thêm `[string]$ConnectionString = ""` (optional). Fallback docker/psql pattern như `verify_cross_docking.ps1` **không** hardcode secret mới vào git nếu chưa có; ưu tiên param.
  2. Helper seed:
     - Chọn 2 `StorageLocations` khác `ZoneId` cùng tenant demo.
     - `INSERT INTO "MobileTasks"` (`Id`,`TenantId`,`ReferenceType`,`ReferenceId`,`Step`,`LocationId`,`AssignedUser`,`Status`,`CreatedAt`,`CreatedBy`) với `CreatedBy='verify_task_interleaving'`, Status Open, Step HIGH/LOW, ReferenceType trong allowlist (`Picking`).
  3. Scenario 4–5: `/next?currentLocationId=&currentZoneId=` → assert distanceScore / priorityScore.
  4. Scenario 9: sau next, SQL `UPDATE task_interleaving.task_recommendations SET "ExpiresAt"=now()-interval '1 minute' WHERE "Id"=...` → accept → 409.
  5. Scenario 10: SQL `UPDATE "MobileTasks" SET "AssignedUser"='other' WHERE "Id"=...` → accept → 409.
  6. Scenario 11–12: reject thiếu reason / đủ reason.
  7. Thiếu ConnectionString & không docker/psql → `SKIPPED: no DB seed channel` (không PASS).
- **Expected Output:** PASS≥12 khi seed được.
- **Validation:** Chạy script 2 lần.
- **Failure Recovery:** Teardown E.2.
- **Continuation Criteria:** FAIL=0.

### Task E.2 - Teardown idempotent
- **Steps:**
  1. Delete/supersede recommendations liên quan seed.
  2. Delete hoặc cancel MobileTasks `CreatedBy='verify_task_interleaving'`.
  3. Unassign nếu để sót AssignedUser.
- **Validation:** Re-run ổn định.
- **Continuation Criteria:** Lần 2 không fail unique/FK.

---

## Phase F - Frontend states + mobile nav
- **Goal:** UI contract section 8+21; parse errorCode (Critic C5).
- **Risk Level:** LOW
- **Dependencies:** Có thể song song backend sau A
- **Expected System State:** Lint xanh

### Task F.0 - Mở rộng http-error payload
- **Primary Target Files:** [http-error.ts](file:///d:/1_Project/48_Nexustock/frontend/src/lib/http-error.ts)
- **Steps:**
  1. Thêm type/helper trả `{ status?, errorCode?, message }` từ axios-like error `response.status` + `response.data.errorCode|message`.
  2. Giữ `getHttpErrorMessage` tương thích.
- **Validation:** Typecheck/lint.
- **Continuation Criteria:** Admin page import được helper mới.

### Task F.1 - Admin unauthorized + featureDisabled
- **Primary Target Files:** [page.tsx](file:///d:/1_Project/48_Nexustock/frontend/src/app/admin/task-interleaving/page.tsx)
- **Steps:**
  1. State: `loading | ready | empty | error | unauthorized | featureDisabled`.
  2. 403 + `TASK_INTERLEAVING_DISABLED` → featureDisabled English copy.
  3. 403 khác → unauthorized.
  4. Empty list → empty; giữ `task-interleaving-refresh-button`.
- **Validation:** `npm run lint`.
- **Continuation Criteria:** 5 states render được.

### Task F.2 - Mobile menu Next task + Sentence case
- **Primary Target Files:**
  - [mobile/page.tsx](file:///d:/1_Project/48_Nexustock/frontend/src/app/mobile/page.tsx)
  - [mobile/tasks/next/page.tsx](file:///d:/1_Project/48_Nexustock/frontend/src/app/mobile/tasks/next/page.tsx)
- **Steps:**
  1. Menu: title `Next task`, href `/mobile/tasks/next`, disabled false.
  2. Copy: `Suggested next task`, `No eligible task found`.
  3. Giữ test ids accept/reject/find-another.
- **Validation:** lint + mở `/mobile`.
- **Continuation Criteria:** Nav visible.

---

## Phase G - Validation gates + docs sync
- **Goal:** DoD thật; docs không over-claim.
- **Risk Level:** LOW
- **Dependencies:** A–F
- **Expected System State:** Phase 29 ✅ chỉ khi gate xanh

### Task G.1 - Gates
- **Steps:**
  1. `dotnet build`
  2. `dotnet test tests/Nexustock.TaskInterleaving.UnitTests`
  3. `npm run lint` (frontend)
  4. `pwsh tests/verify_task_interleaving.ps1` (+ ConnectionString khi có)
- **Validation:** FAIL=0; unit 100%; lint 0 error; với seed PASS≥12.
- **Failure Recovery:** Không tick ✅; giữ `🔄`.
- **Continuation Criteria:** Tất cả xanh.

### Task G.2 - Docs
- **Primary Target Files:**
  - [phase_29_task_interleaving.md](file:///d:/1_Project/48_Nexustock/planning/phases/phase_29_task_interleaving.md)
  - [IMPLEMENTATION_PLAN.md](file:///d:/1_Project/48_Nexustock/planning/IMPLEMENTATION_PLAN.md)
  - Mirror: [phase_29_gap_fix_plan.md](file:///d:/1_Project/48_Nexustock/planning/phases/phase_29_gap_fix_plan.md) (ghi đã execute / evidence)
- **Steps:**
  1. Sau G.1: status ✅ ngày 2026-07-21; evidence build/lint/unit/verify counts thật.
  2. Xóa residual gap list; sync exit gate section 24; bỏ mâu thuẫn ngày.
- **Validation:** Docs khớp evidence.
- **Continuation Criteria:** `rp5` re-check = 100%.

---

# IMPLEMENTATION ORDER

```text
1. A.1 cleanup SQL
2. A.2 migration unique + precision
3. A.3 supersede GetNext
4. A.4 Accept commit-safe catch
5. B.1 scorer
6. B.2 wire scorer
7. C.1 logs
8. D.1 unit tests + sln
9. F.0 http-error → F.1 admin → F.2 mobile
10. E.1–E.2 verify seed
11. G.1 gates
12. G.2 docs ✅
```

---

# CRITIC RECONCILIATION LOG

| ID | Verdict | Plan change |
|---|---|---|
| C1 | ACCEPTED | A.1 SQL verbatim + gate trước migrate |
| C2 | ACCEPTED | A.3 supersede trong TX trước insert |
| C3 | ACCEPTED | **NEW Task A.4** Accept catch |
| C4 | ACCEPTED | E.1 MobileTasks schema + 2 Zone locations |
| C5 | ACCEPTED | **NEW Task F.0** http-error errorCode |
| H1 | ACCEPTED | D.1 `dotnet sln add` |
| H2 | ACCEPTED | public Scorer |
| H3 | ACCEPTED | Filter `"Status"` PascalCase |
| H4 | ACCEPTED | SKIP khi không seed; PASS≥12 có điều kiện |
| M3 | ACCEPTED | SelectedScore precision trong A.2 |
| API expire | REJECTED | Giữ SQL verify |

---

# FINAL VALIDATION CHECKLIST

- [x] A.1 cleanup duplicate Open = 0 row
- [x] A.2 index `uq_recommendations_tenant_user_open` live; SelectedScore numeric(18,4)
- [x] A.3 hai lần `/next` không 23505; ≤1 Open/user
- [x] A.4 accept expired/conflict không secondary rollback error
- [x] B scoring: 45/35/10/20 + age/priority/continuity/penalty đúng plan
- [x] C đủ 6 event log names
- [x] D `dotnet test` unit 100%; project trong `Nexustock.sln`
- [x] E verify FAIL=0; với ConnectionString PASS≥12; scenario 4–5,7–12 không SKIP
- [x] F.0 errorCode parse được
- [x] F.1 admin: loading/ready/empty/error/unauthorized/featureDisabled
- [x] F.2 mobile hub có `Next task`; copy Sentence case
- [x] G.1 build + lint + unit + verify xanh
- [x] G.2 phase_29 + IMPLEMENTATION_PLAN ✅ + evidence khớp
- [x] Không đổi permission/flag/API route names
- [x] Không đụng ledger/allocation/wave rules

---

# ROLLBACK

1. Tắt `FF_TASK_INTERLEAVING_ENABLED`.
2. Revert code gap-fix.
3. Migration Down: drop `uq_recommendations_tenant_user_open`.
4. Không xóa recommendation logs production.

---

# SUCCESS DEFINITION

Phase 29 = **100%** chỉ khi checklist trên đủ và evidence verify không dựa SKIP hàng loạt.

---

# NEXT (ngoài phạm vi workflow này)

Workflow `/17-auto-plan` **STOP tại đây**. Execution chỉ khi FOUNDER gọi `/04-do-plan` hoặc `tt`.
