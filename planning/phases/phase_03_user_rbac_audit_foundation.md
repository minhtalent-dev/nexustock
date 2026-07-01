# PHASE 03: User, RBAC & audit foundation

## 1. Mục tiêu

Thiết lập nền bảo mật cho Nexustock WMS: user, role, permission, tenant/warehouse scope, session/JWT và audit log cho mọi thay đổi quan trọng.

Phase này là cổng kiểm soát bắt buộc cho toàn bộ mutation API từ phase 04 trở đi.

## 2. Phạm vi

### In scope

* Tạo module Identity/RBAC/Audit.
* Tạo user, role, permission catalog, user-role, role-permission.
* Seed admin role và permission catalog nền.
* Chuẩn hóa policy name `{domain}.{action}`.
* Cấu hình password policy và JWT/session từ env.
* Tạo audit middleware/service cho mutation.
* Tạo menu visibility theo quyền.
* Enforce tenant scope và warehouse scope.

### Out of scope

* SSO.
* MFA.
* Fine-grained row-level security phức tạp.
* Device pairing security.
* Approval workflow nâng cao.

## 3. Dependency

| Loại | Chi tiết |
|---|---|
| Upstream | Phase 01-02 |
| Downstream trực tiếp | Phase 04-30 |
| Contract tạo ra | Auth/session, permission catalog, tenant/warehouse scope, audit log, security error behavior |
| Enterprise reference | [Security model](../enterprise/security_model.md), [API contracts core](../enterprise/api_contracts_core.md), [Measurable acceptance criteria](../enterprise/measurable_acceptance_criteria.md) |

## 4. Security baseline

### Tenancy and warehouse scope

* `tenantId` bắt buộc trong user context.
* User có thể được cấp nhiều `warehouseId` trong cùng tenant.
* API warehouse-scoped phải kiểm tra user có quyền trên `warehouseId` đó.
* Cross-tenant access trả 404 hoặc 403 theo policy, không leak record tồn tại.
* Không tin `tenantId` từ client nếu token/session đã có tenant context.

### Permission convention

| Pattern | Ví dụ |
|---|---|
| `{domain}.read` | `inventory.read` |
| `{domain}.create` | `inboundReceiving.create` |
| `{domain}.update` | `masterData.item.update` |
| `{domain}.approve` | `cycleCount.approve` |
| `{domain}.export` | `masterData.export` |
| `{domain}.admin` | `rbac.admin` |

## 5. Database

| Table | Required fields | Main constraints | Indexes |
|---|---|---|---|
| `Users` | id, tenantId, userName, email, passwordHash, displayName, status, lastLoginAt | unique tenantId+userName, unique tenantId+email | tenantId+status |
| `Roles` | id, tenantId, roleCode, roleName, status, isSystemRole | unique tenantId+roleCode | tenantId+status |
| `Permissions` | id, permissionCode, module, action, description, status | unique permissionCode | module+action |
| `UserRoles` | id, tenantId, userId, roleId | unique userId+roleId | tenantId+userId |
| `RolePermissions` | id, tenantId, roleId, permissionId | unique roleId+permissionId | tenantId+roleId |
| `UserWarehouseAccess` | id, tenantId, userId, warehouseId, status | unique userId+warehouseId | tenantId+warehouseId |
| `Sessions` | id, tenantId, userId, tokenHash, expiresAt, revokedAt | tokenHash unique | userId+expiresAt |
| `AuditLogs` | id, tenantId, warehouseId, actorUserId, entityName, entityId, action, oldValue, newValue, reasonCode, traceId, createdAt | append-only | tenantId+entityName+entityId, tenantId+traceId |
| `SecurityEvents` | id, tenantId, userId, eventType, ipHash, userAgentHash, traceId, createdAt | append-only | tenantId+eventType+createdAt |

### Database rules

* Password/token lưu hash, không lưu raw value.
* Audit log append-only.
* `Permissions.permissionCode` immutable sau khi seed.
* `AuditLogs.oldValue/newValue` phải mask secret và dữ liệu nhạy cảm.
* Role system không được xóa; chỉ inactive nếu có migration/control rõ.

## 6. Backend/API

| API | Mục đích | Permission/Auth | Ghi chú |
|---|---|---|---|
| `POST /api/auth/login` | Đăng nhập | Public | Không log password; trả lỗi chung |
| `POST /api/auth/logout` | Đăng xuất | Auth | Revoke session nếu có |
| `GET /api/me` | User context | Auth | tenantId, warehouse access, displayName |
| `GET /api/me/permissions` | Lấy quyền hiện tại | Auth | Dùng cho UI menu |
| `GET /api/users` | Danh sách user | `user.read` | Paging/filter |
| `POST /api/users` | Tạo user | `user.create` | Audit |
| `PATCH /api/users/{id}/status` | Active/inactive user | `user.update` | Requires reason |
| `POST /api/roles` | Tạo role | `role.create` | Audit |
| `POST /api/roles/{id}/permissions` | Gán quyền | `role.assignPermission` | Audit old/new |
| `POST /api/users/{id}/warehouses` | Gán kho cho user | `user.assignWarehouse` | Same tenant only |
| `GET /api/audit-logs` | Tra cứu audit | `audit.read` | Filter entity/user/time |

### API rules

* Request/response dùng camelCase.
* Mutation API bắt buộc auth, permission và audit.
* 401 cho chưa đăng nhập; 403 cho thiếu quyền.
* Không trả `passwordHash`, `tokenHash`, raw token hoặc secret.
* Response lỗi chuẩn gồm `errorCode`, `message`, `details`, `traceId`.
* Không trả dữ liệu tenant khác, kể cả khi biết id.

## 7. Frontend/RF/mobile

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| Login page | Đăng nhập | Hiển thị lỗi chung, không lộ user tồn tại |
| User management | Quản lý người dùng | Status, role assignment, warehouse access |
| Role management | Quản lý vai trò | Permission matrix theo module/action |
| Audit viewer | Tra cứu audit | Filter user, entity, action, time, traceId |
| Unauthorized state | Chặn truy cập | Message rõ, không crash route |

### UI rules

* UI text dùng Sentence case.
* Không dùng inline style.
* Không kiểm quyền chỉ ở UI; UI chỉ ẩn/disable để UX tốt hơn.
* Permission matrix phải có search/filter theo module.
* Action đổi quyền/user status phải có confirm.

## 8. Execution flow

1. System seed permission catalog.
2. System seed admin role.
3. Admin tạo role nghiệp vụ.
4. Admin gán permission vào role.
5. Admin tạo user và gán role + warehouse access.
6. User đăng nhập.
7. Frontend lấy `/api/me/permissions` để dựng menu.
8. API enforce policy cho mỗi mutation.
9. Audit log ghi actor, entity, action, old/new, traceId.

## 9. Validation & business rules

* Mọi API mutation phải auth.
* Mọi API mutation phải enforce permission ở backend.
* Password policy đọc từ config, có minimum length và complexity tối thiểu.
* Sai credential trả lỗi chung, không nói user hay password sai.
* User inactive không được login.
* Token hết hạn hoặc revoked không dùng được.
* Permission code immutable sau khi seed.
* User chỉ được thao tác warehouse đã được cấp.
* Không cho admin tự remove quyền cuối cùng làm hệ thống mất admin nếu không có break-glass plan.
* Audit không được chứa password/token/secret.

## 10. Exception handling

| Lỗi | Hành vi hệ thống |
|---|---|
| Sai credential | Trả lỗi chung, ghi security event |
| User inactive | Trả lỗi chung hoặc account disabled message theo policy |
| Role thiếu permission | Trả 403 |
| Token hết hạn | Trả 401 |
| Cross-tenant entityId | Trả 404/403, ghi security event nếu nghi ngờ tampering |
| Dữ liệu stale khi đổi role | Trả 409, yêu cầu reload |
| Permission không tồn tại | Trả validation error |

## 11. Observability

* Audit login/logout thành công và thất bại.
* Audit đổi role, đổi permission, đổi warehouse access, đổi user status.
* Trace ID trên mọi request.
* Security event khi login fail nhiều lần hoặc cross-tenant probing.
* Log không chứa password, token, secret hoặc dữ liệu nhạy cảm không mask.

## 12. Test plan

| Nhóm test | Nội dung |
|---|---|
| Unit | Permission resolution, password policy, audit masking |
| Integration | Login, 401/403/200, role assignment, audit write |
| Security | Không trả passwordHash/tokenHash, cross-tenant blocked |
| E2E | Login, menu visibility, unauthorized route |
| Negative | Inactive user, expired token, stale role update |
| Regression | Phase 01-02 health/master data vẫn hoạt động |

## 13. Measurable acceptance criteria

* Unauthorized mutation returns 401/403 đúng trường hợp.
* User thiếu permission không thể gọi mutation API dù UI bị bypass.
* Role permission change writes audit row with old/new value masked.
* `/api/me/permissions` trả permission đúng role và dùng camelCase.
* User chỉ thấy/thao tác warehouse được cấp.
* API không bao giờ trả passwordHash, tokenHash hoặc raw secret.
* Login fail nhiều lần được ghi security event.
* Audit query filter được theo actor, entity, action, traceId và time range.

## 14. Definition of done

* Database migration chạy sạch trên database trống.
* API auth/RBAC/audit có integration test pass.
* UI login/user/role/audit flow thao tác được end-to-end.
* Audit/trace hoạt động cho command quan trọng.
* Security negative path chính được test.
* README hoặc phase note đủ để executor phase 04 dùng permission/audit.
* Không còn placeholder generic trong phần triển khai phase.

## 15. Maintenance notes

* Permission mới phải thêm vào catalog và phase tương ứng.
* Không kiểm quyền chỉ ở UI.
* Audit schema không chứa dữ liệu nhạy cảm không mask.
* Nếu đổi permission convention, cập nhật toàn bộ phase downstream.
* Nếu thêm auth mode mới, cập nhật security model.

## 16. Rollback notes

* Disable user/role thay vì xóa khi có dữ liệu liên quan.
* Rollback permission seed bằng migration nếu chưa được dùng.
* Khôi phục role assignment từ audit nếu gán sai.
* Không xóa audit/security event production.
