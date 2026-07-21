# EXECUTIVE REVIEW SUMMARY
- **Safe Execution Score:** 7.5/10 (draft) → sau refine kỳ vọng 9.0/10
- **Final Verdict:** APPROVED WITH CHANGES
- **Scope:** Phase 29 gap fix A→G — áp dụng hết 7 residual gaps (`fp`)
- **Repo:** `d:\1_Project\48_Nexustock`
- **Ngày review:** 2026-07-21

# CRITICAL ISSUES

## C1 — Unique index fail nếu cleanup thiếu trước migrate
- **Severity:** CRITICAL
- **Affected Area:** Phase A Task A.1–A.2
- **Execution Risk:** `dotnet ef database update` fail; API `/next` 500 sau deploy nửa chừng
- **Why It Matters:** DB local đã có nhiều Open recommendation từ verify trước
- **Recommended Fix:** Bắt buộc SQL cleanup + verify query `HAVING COUNT(*)>1 = 0` trước khi apply migration; ghi SQL vào plan verbatim

## C2 — GetNext tạo Open mới sau unique → 23505
- **Severity:** CRITICAL
- **Affected Area:** Phase A Task A.3
- **Execution Risk:** Mọi lần `/next` thứ 2 trở đi fail unique violation
- **Why It Matters:** Core UX RF/mobile
- **Recommended Fix:** Cùng transaction: expire Open hết hạn → supersede Open còn hiệu lực → insert mới → SaveChanges một lần

## C3 — Accept catch Rollback sau Commit (expired/conflict)
- **Severity:** HIGH
- **Affected Area:** Existing `AcceptAsync` + Phase C logs
- **Execution Risk:** `RollbackAsync` sau `CommitAsync` có thể throw secondary exception che mất error code thật
- **Why It Matters:** Scenario expired/conflict verify sẽ flaky
- **Recommended Fix:** Refactor try/catch: chỉ Rollback khi chưa commit; hoặc rethrow business exception ngoài khối catch Rollback

## C4 — Verify seed sai schema/cột MobileTasks
- **Severity:** HIGH
- **Affected Area:** Phase E
- **Execution Risk:** Seed SQL fail → vẫn SKIP hàng loạt → DoD giả
- **Why It Matters:** Bảng `MobileTasks` (public schema), cột PascalCase theo EF snapshot; cần `LocationId` thật từ `StorageLocations` khác ZoneId
- **Recommended Fix:** Seed helper: SELECT 2 location khác ZoneId trước; INSERT MobileTasks với `CreatedBy='verify_task_interleaving'`; teardown DELETE/UPDATE theo tag

## C5 — http-error.ts không expose errorCode
- **Severity:** HIGH
- **Affected Area:** Phase F Admin featureDisabled
- **Execution Risk:** Không phân biệt `TASK_INTERLEAVING_DISABLED` vs Forbid permission → state sai
- **Recommended Fix:** Mở rộng `getHttpErrorMessage` hoặc thêm `getHttpErrorPayload` đọc `response.data.errorCode` + `status`; map 403+DISABLED → featureDisabled

# HIGH ISSUES

## H1 — Unit test project không vào solution
- **Severity:** HIGH
- **Affected Area:** Phase D
- **Execution Risk:** `dotnet test` ở root bỏ sót
- **Recommended Fix:** `dotnet sln Nexustock.sln add tests/Nexustock.TaskInterleaving.UnitTests/...csproj`

## H2 — Scorer phải public; InternalsVisibleTo không đủ nếu quên
- **Severity:** HIGH
- **Affected Area:** Phase B/D
- **Recommended Fix:** `public static class TaskInterleavingScorer` trong namespace Services

## H3 — Column filter casing Status
- **Severity:** HIGH
- **Affected Area:** Phase A index
- **Execution Risk:** Filter `"status"` vs `"Status"` lệch snapshot → index không match query
- **Recommended Fix:** Dùng `"Status"` khớp migration TaskInterleaving hiện có (PascalCase quoted)

## H4 — PASS giả khi thiếu ConnectionString
- **Severity:** HIGH
- **Affected Area:** Phase E
- **Recommended Fix:** Thiếu ConnectionString → scenario seedable = SKIPPED rõ; không assert yếu thành PASS; gate G yêu cầu PASS≥12 chỉ khi có ConnectionString

# MEDIUM ISSUES

## M1 — Continuity +4 session vs location
- Plan đã khóa session context — OK; document regression: ranking có thể đổi

## M2 — Mobile menu text language
- Mobile hub hiện bilingual VN; contract phase yêu cầu label English `Next task` — OK; description ngắn English

## M3 — SelectedScore precision numeric vs decimal(18,4)
- Optional harden trong cùng migration A.2: `HasPrecision(18,4)` cho SelectedScore

## M4 — Worker user scenario 2
- Nếu `worker@nexustock.com` không tồn tại → SKIP hợp lệ; không FAIL gate nếu đã document

# ARCHITECTURAL WEAKNESSES & RISKS
- **Security Gaps:** Seed SQL không được log password; ConnectionString qua param/env
- **Scalability Gaps:** GetNext vẫn load toàn bộ Open MobileTasks — chấp nhận Phase 29; không mở rộng scope
- **Reliability Concerns:** Accept Commit+throw (C3); unique Open (C1/C2)

# FILE & TASK GRANULARITY RISKS
- **Oversized Tasks:** B.1+B.2 OK nếu tách file scorer trước wire
- **Vague File Targeting:** Đã siết path tuyệt đối trong plan refine
- **Missing from draft:** `Nexustock.sln`, `frontend/src/lib/http-error.ts`, schema `MobileTasks` columns

# RECONCILIATION DIRECTIVE FOR PHASE 3
1. ACCEPTED C1–C5, H1–H4 → harden steps + checklist  
2. ACCEPTED M3 as optional in A.2  
3. REJECTED mở rộng API expire — giữ SQL verify  
4. REJECTED đổi unique sang include ExpiresAt trong filter (NOW() không immutable)
