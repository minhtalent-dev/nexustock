# PHASE 03: User, RBAC & audit foundation

## Execution spec maturity

- **Mức hiện tại:** 100% (Prod-ready)
- **Đánh giá:** Hoàn tất bổ sung chi tiết kiến trúc Identity, JWT Auth, Refresh Token Rotation, Centralized Audit Log và Tenant Resolution.
- **Khi cần upgrade:** Không cần thiết, trừ khi chuyển sang mô hình Identity Server độc lập hoặc SSO/MFA nâng cao.

## 1. Mục tiêu

Thiết lập bảo mật nền: user, role, permission, JWT/session và audit log cho mọi thay đổi dữ liệu.

Phase này thuộc stage **MVP vận hành chắc** và phải tạo ra deliverable có thể kiểm thử độc lập. Nội dung phải đủ rõ để executor triển khai mà không cần suy đoán nghiệp vụ chính.

## 2. Phạm vi

Identity API, permission catalog, role assignment, audit middleware, menu visibility theo quyền.

### In scope

* [x] Tạo module Identity (`Nexustock.Modules.Identity`) và tích hợp vào Host API.
* [x] Tích hợp ASP.NET Core Identity dùng EF Core với PostgreSQL.
* [x] Seed admin role và permission catalog (MasterData & Identity).
* [x] Chuẩn hóa policy name dạng `ModuleName.Feature.Action` (nhất quán với MasterData).
* [x] Cấu hình password policy, JWT authentication & token validation middleware.
* [x] Cấu hình tích hợp JWT Bearer Token trong Swagger UI.
* [x] Cơ chế Refresh Token Rotation chống replay attack.
* [x] Thiết lập SaveChangesInterceptor ghi nhận AuditLog tập trung.
* [x] Giải pháp claims-based tenant resolution tích hợp vào `TenantProvider`.

### Non-negotiable output

* Có database contract rõ ràng bao gồm cả cấu hình thực thể Identity.
* Có cấu hình chi tiết middleware và DI tại Host.
* Có UI/RF/mobile touchpoint nếu người dùng vận hành trực tiếp.
* Có execution flow end-to-end.
* Có validation, exception, observability và test plan.

## 3. Điều kiện đầu vào

Phase 01-02 hoàn tất.

### Readiness checklist

* [x] Phase phụ thuộc đã pass acceptance criteria.
* [x] Master data tối thiểu đã có nếu phase cần dữ liệu vận hành.
* [x] Permission liên quan đã được seed hoặc có kế hoạch seed.
* [x] Không còn migration pending từ phase trước.
* [x] Các status lifecycle liên quan đã được thống nhất trong tài liệu phase trước.

## 4. Setup

### Cấu trúc module thực tế

```text
backend/modules/Nexustock.Modules.Identity/
frontend/src/features/identity/
planning/phases/phase_03_user_rbac_audit_foundation.md
```

### 4.1 Cấu hình ASP.NET Core Identity & JWT Authentication

#### Cấu hình ASP.NET Core Identity
Đăng ký Identity trong `Nexustock.Modules.Identity` sử dụng Npgsql:
- **User Entity**: `ApplicationUser : IdentityUser<Guid>` bổ sung các trường: `TenantId (Guid)`, `FullName (string)`, `IsActive (bool)`.
- **Role Entity**: `ApplicationRole : IdentityRole<Guid>` bổ sung các trường: `TenantId (Guid)`, `Description (string)`.
- **Password Policy**:
  - `Password.RequireDigit = true`
  - `Password.RequiredLength = 8`
  - `Password.RequireNonAlphanumeric = true`
  - `Password.RequireUppercase = true`
  - `Password.RequireLowercase = true`

#### Cấu hình JWT Validation tại Host (`Nexustock.Api/Program.cs`)
Đăng ký Authentication và Authorization middleware:
```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT_ISSUER"],
        ValidAudience = builder.Configuration["JWT_AUDIENCE"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT_SECRET_KEY"]!)),
        ClockSkew = TimeSpan.Zero
    };
});
```
Thứ tự Pipeline bắt buộc:
```csharp
app.UseRouting();
app.UseCors("AllowFrontendDev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

#### Tích hợp Swagger JWT UI
Cấu hình SwaggerGen để hiển thị nút "Authorize" hỗ trợ gửi kèm JWT Bearer:
```csharp
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
```

### Permission seed đề xuất

* **Identity permissions:**
  * `Identity.Users.View`
  * `Identity.Users.Create`
  * `Identity.Users.Edit`
  * `Identity.Users.Delete`
  * `Identity.Roles.View`
  * `Identity.Roles.Edit`
  * `Identity.Audit.View`

Chỉ seed permission thực sự dùng trong phase. Không tạo quyền dư nếu chưa có màn hình hoặc API tương ứng.

## 5. Database

| Thành phần dữ liệu | Mục đích | Ràng buộc chính |
|---|---|---|
| `Users` | Tài khoản người dùng | Unique tenantId+userName/email, passwordHash, status |
| `Roles` | Vai trò | Unique tenantId+roleCode |
| `Permissions` | Catalog quyền | Unique permissionCode dạng Module.Feature.Action |
| `UserRoles` | Gán role | Unique userId+roleId |
| `RolePermissions` | Gán quyền | Unique roleId+permissionId |
| `UserRefreshTokens` | Lưu refresh token | Unique Token, Foreign Key userId, ExpiresAt, RevokedAt |
| `AuditLogs` | Nhật ký thay đổi | entityName, entityId, action, oldValue, newValue, traceId, tenantId |

### Chuẩn database áp dụng

* Mọi bảng nghiệp vụ có `id`, `tenantId`, `createdAt`, `createdBy`, `updatedAt`, `updatedBy` nếu có chỉnh sửa.
* Bảng transaction bất biến không cho update nội dung tài chính/tồn kho sau khi commit; nếu sai dùng corrective transaction.
* Index tối thiểu theo `tenantId`, `code/reference`, `status`, `createdAt` và khóa ngoại hay dùng để query.
* Dữ liệu số lượng dùng decimal precision thống nhất, không dùng floating point.
* Status lưu bằng enum/string ổn định, không lưu text tự do.
* Migration phải có rollback strategy hoặc ghi rõ lý do không rollback an toàn.

### 5.1 Kiến trúc AuditLog tập trung

Để đảm bảo tính độc lập giữa các Module (Modular Monolith) nhưng vẫn có thể lưu AuditLog tập trung vào DB chung:
- **SaveChangesInterceptor**: Viết một class `AuditInterceptor : SaveChangesInterceptor` kế thừa của EF Core đặt tại lớp Infrastructure dùng chung.
- **Cơ chế hoạt động**:
  1. Khi một DbContext thực hiện `SaveChanges` hoặc `SaveChangesAsync`, Interceptor sẽ bắt sự kiện và lọc ra các thực thể bị thay đổi (Added, Modified, Deleted).
  2. Tạo bản ghi `AuditLog` với `OldValue` và `NewValue` ở dạng JSON.
  3. Lấy thông tin `TenantId` từ `ITenantProvider`, `UserId` từ `ClaimsPrincipal` và `TraceId` từ `System.Diagnostics.Activity`.
  4. Lưu các bản ghi này vào DB. Để cô lập transaction, các Module DbContext khác sẽ ghi trực tiếp bản ghi AuditLog vào DB qua kết nối chung hoặc publish một Domain Event qua MediatR/Outbox pattern để Identity Module xử lý việc lưu vào bảng `AuditLogs`.

### Transaction boundary

* Mọi thay đổi inventory hoặc trạng thái quan trọng phải nằm trong một transaction.
* Không gọi hệ thống ngoài trong DB transaction dài.
* Nếu cần publish event, dùng outbox/integration log sau commit.
* Chống double-submit bằng idempotency key ở command quan trọng.

## 6. Backend/API

| API | Phương thức | Mục đích | Ghi chú triển khai |
|---|---|---|---|
| `/api/auth/login` | POST | Đăng nhập | Trả về Access Token, Refresh Token. Không log password |
| `/api/auth/refresh-token` | POST | Refresh token | Nhận Refresh Token cũ, trả về Access Token + Refresh Token mới |
| `/api/auth/revoke-token` | POST | Thu hồi refresh token | Revoke Refresh Token hiện tại |
| `/api/users` | GET | Danh sách user | Yêu cầu `Identity.Users.View` |
| `/api/users` | POST | Tạo user | Yêu cầu `Identity.Users.Create` |
| `/api/roles/{id}/permissions` | POST | Gán quyền | Yêu cầu `Identity.Roles.Edit` |
| `/api/me/permissions` | GET | Lấy quyền hiện tại | Dùng cho UI menu |

### 6.1 Claims-based Tenant Resolution

Khi user đăng nhập thành công, JWT Payload sẽ chứa thông tin tenant của user đó:
```json
{
  "sub": "user_id_guid",
  "email": "user@email.com",
  "tenant_id": "tenant_id_guid",
  "exp": 1719999999
}
```
Tại `TenantProvider.cs` trong các Module, sửa lại logic lấy `TenantId` động từ claims của `HttpContext`:
```csharp
public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;
            return tenantClaim != null ? Guid.Parse(tenantClaim) : Guid.Parse("00000000-0000-0000-0000-000000000001");
        }
    }
}
```

### Quy chuẩn API

* Request/response dùng camelCase.
* Mutation API bắt buộc auth và permission.
* Response lỗi chuẩn gồm `errorCode`, `message`, `details`, `traceId`.
* Query API có pagination mặc định và max page size.
* Command API validate input tại boundary trước khi vào domain logic.
* Không trả dữ liệu tenant khác, kể cả khi biết id.

### Service layer

* Controller chỉ nhận request, validate model state, gọi application service.
* Application service điều phối transaction, permission, idempotency.
* Domain service xử lý rule nghiệp vụ thuần.
* Repository/query tách riêng command và read model khi query phức tạp.

## 7. Frontend/RF/mobile

| Màn hình/Control | Mục đích | Yêu cầu UX |
|---|---|---|
| Login page | Đăng nhập | Hiển thị lỗi chung, không lộ user tồn tại |
| User management | Quản lý người dùng | Status, role assignment |
| Role management | Quản lý vai trò | Permission matrix |
| Audit viewer | Tra cứu audit | Filter user, entity, action, time |

### Chuẩn UI áp dụng

* UI text dùng Sentence case.
* Không dùng inline style.
* Sử dụng Next.js, Tailwind CSS và Shadcn UI. Không dùng inline style, tuân thủ component/style nhất quán.
* Mọi action nguy hiểm có confirm rõ ràng.
* Mọi màn hình có loading, empty, error, unauthorized state.
* Bảng dữ liệu có filter, pagination và trạng thái no result.
* RF/mobile ưu tiên input scan auto-focus, font lớn, ít nút, phản hồi rõ.

### State cần hiển thị

* Draft/open/in progress/completed/cancelled nếu phase có workflow.
* Locked/blocked/exception nếu thao tác bị chặn.
* Last updated và actor cho dữ liệu quan trọng.
* Trace ID hoặc reference ID khi cần hỗ trợ vận hành.

## 8. Execution flow

1. Admin tạo role
2. Gán permission
3. Tạo user
4. User đăng nhập (nhận Access Token & Refresh Token)
5. Frontend lưu trữ Token và tự động refresh trước khi hết hạn
6. Frontend lấy danh sách permission gán cho Menu
7. API enforce policy dùng JwtBearer Auth
8. Interceptor ghi nhận AuditLog khi có thay đổi dữ liệu

### Flow guardrails

* Không bỏ qua bước validate master data.
* Không tự động sửa tồn kho nếu chưa có transaction hợp lệ.
* Không ghi đè trạng thái mới hơn bằng dữ liệu cũ.
* Nếu flow có scan, mọi scan phải gắn context nghiệp vụ.
* Nếu flow có approval, người tạo và người duyệt nên tách quyền khi nghiệp vụ yêu cầu.

## 9. Validation & business rules

* Mọi API mutation phải auth
* 401 cho chưa đăng nhập, 403 cho thiếu quyền
* Password hash an toàn qua ASP.NET Core Identity PasswordHasher
* Không log token/password/secret
* Permission code immutable sau khi seed
* **Refresh Token Rotation Rules**:
  * Mỗi Refresh Token chỉ được dùng 1 lần duy nhất để lấy cặp token mới.
  * Nếu một Refresh Token đã sử dụng lại được gửi lên (Replay attack), hệ thống lập tức thu hồi/vô hiệu hóa toàn bộ Refresh Tokens hoạt động của User đó, bắt đăng nhập lại.

### Validation nền bắt buộc

* Validate tenant scope.
* Validate status transition.
* Validate permission theo action.
* Validate optimistic concurrency cho dữ liệu dễ tranh chấp.
* Validate số lượng không âm và không vượt khả dụng khi liên quan tồn kho.
* Validate reason code bắt buộc cho override, reject, cancel hoặc adjustment.

## 10. Exception handling

* Sai credential
* User inactive
* Role thiếu permission
* Token hết hạn
* Tampering entityId ngoài tenant

### Mapping lỗi chuẩn

| Nhóm lỗi | Hành vi hệ thống |
|---|---|
| Input sai | Trả validation error, không ghi transaction |
| Thiếu quyền | Trả 403, ghi security audit nếu cần |
| Dữ liệu stale | Trả conflict, yêu cầu reload |
| Vi phạm rule kho | Block hoặc tạo operational exception theo severity |
| Lỗi thiết bị/tích hợp | Ghi integration/device log, cho retry hoặc fallback nếu an toàn |
| Lỗi không khôi phục | Ghi trace ID, rollback transaction, báo admin |

### Nguyên tắc exception

* Lỗi vận hành có thể xử lý nghiệp vụ thì tạo exception framework.
* Lỗi kỹ thuật chỉ tạo operational exception nếu ảnh hưởng tác vụ kho.
* Không nuốt lỗi âm thầm.
* Mọi override phải có reason và audit.

## 11. Observability

* Audit login/logout thất bại/thành công
* Audit đổi quyền
* Trace ID trên mọi request
* Security alert nếu login fail nhiều lần

### Log và trace

* Mỗi request có trace ID.
* Command quan trọng ghi audit log.
* Entity nghiệp vụ chính ghi activity timeline.
* Job nền và integration event truyền trace ID khi liên quan flow gốc.
* Log không chứa password, token, secret hoặc dữ liệu nhạy cảm không mask.

### KPI đề xuất

* Throughput theo ngày/ca/user nếu phase có thao tác vận hành.
* Aging của task mở hoặc exception mở.
* Tỷ lệ lỗi validation/rule block.
* Tỷ lệ retry/failure nếu phase có tích hợp.
* Độ chính xác tồn kho nếu phase ảnh hưởng inventory.

## 12. Test plan

* Login success/fail
* API 401/403/200
* Menu ẩn theo quyền
* Audit ghi old/new value
* Không trả passwordHash
* Tái sử dụng Refresh Token cũ (để kiểm tra xem hệ thống có block/vô hiệu hóa các tokens khác không)

### Test matrix bắt buộc

| Nhóm test | Nội dung |
|---|---|
| Unit | Rule nghiệp vụ, status transition, validation helper |
| Integration | API + DB transaction + permission + concurrency |
| E2E | Luồng người dùng chính từ UI/RF/mobile |
| Negative | Sai quyền, sai trạng thái, dữ liệu stale, duplicate request |
| Regression | Không phá phase trước và dependency downstream |

### Dữ liệu test

* Tenant demo.
* User đủ quyền và user thiếu quyền.
* Master data hợp lệ và master data inactive.
* Bản ghi đang open/completed/cancelled để test transition.
* Dữ liệu conflict/concurrency nếu phase ghi transaction.

## 13. Acceptance criteria

* [x] RBAC chặn được mutation trái quyền
* [x] Audit đủ truy vết ai sửa gì lúc nào

### Definition of done

* [x] Database migration chạy sạch trên database trống.
* [x] API chính có test integration pass.
* [x] UI/RF/mobile flow chính thao tác được end-to-end.
* [x] Audit/trace hoạt động cho command quan trọng.
* [x] Exception path chính được test.
* [x] README hoặc phase note đủ để executor tiếp theo hiểu dependency.
* [x] Không còn placeholder generic trong phần triển khai phase.

## 14. Out of scope

* SSO
* MFA
* Fine-grained row-level security phức tạp

Không đưa scope ngoài vào phase này nếu chưa có dependency rõ. Nếu phát hiện scope mới bắt buộc, cập nhật roadmap tổng trước khi triển khai.

## 15. Dependencies

* Phase 01-02

### Downstream impact

* Phase sau được phép dùng API/status/data contract của phase này.
* Nếu đổi contract sau khi phase đã hoàn tất, phải cập nhật phase phụ thuộc.
* Không đổi tên bảng/API đã được phase sau tham chiếu nếu không có migration plan.

## 16. Maintenance notes

* Permission mới phải thêm vào catalog và plan phase tương ứng
* Không kiểm quyền chỉ ở UI
* Audit schema không chứa dữ liệu nhạy cảm không mask

### Maintenance contract

* Giữ section tài liệu này đồng bộ với migration/API thực tế.
* Khi thêm status mới, cập nhật validation, UI badge, test và exception mapping.
* Khi thêm permission mới, cập nhật seed, UI visibility và API policy.
* Khi thêm field bắt buộc, cập nhật import/export, DTO, validation và test data.

## 17. Extension points

* Thêm SSO
* Thêm MFA
* Thêm approval workflow đổi quyền

### Nguyên tắc mở rộng

* Mở rộng bằng module hoặc service rõ ràng, không nhét logic vào controller.
* Ưu tiên cấu hình/rule trước khi hardcode nghiệp vụ mới.
* Không thêm dependency ngoài nếu standard library hoặc dependency hiện có xử lý đủ.
* Feature nâng cao nên có permission hoặc feature flag riêng.

## 18. Rollback notes

* Disable user/role thay vì xóa
* Rollback permission seed bằng migration
* Khôi phục role assignment từ audit nếu gán sai

### Rollback safety

* Không xóa transaction đã phát sinh trong production.
* Nếu dữ liệu sai, tạo corrective transaction hoặc trạng thái hủy có audit.
* Nếu UI lỗi, có thể ẩn menu/permission tạm thời.
* Nếu API lỗi, rollback deployment image trước, xử lý dữ liệu sau theo trace ID.
