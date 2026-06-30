# PHASE 3: REINDEX MODULES CŨ & PHÁT TRIỂN CORE API BACKEND (REINDEX & BACKEND SERVICES)

Phase này phân tích chuyên sâu (Reindex) mã nguồn của các module nghiệp vụ trong các dự án cũ để sàng lọc, loại bỏ logic hardcode, khắc phục các bug lịch sử, tích hợp hệ thống phân quyền tài khoản chi tiết, quy trình kiểm kê định kỳ, phân hoạch vùng kho bảo quản, thuật toán cất hàng tối ưu (Putaway Slotting), quy trình chuyển tiếp trực tiếp (Cross-Docking), gom đơn hàng xuất kho (Wave Picking), cây gia phả truy vết chất lượng (Material Genealogy), đo lường năng suất lao động (Labor Tracking), lịch hẹn cửa kho (Dock Scheduling), cấu hình cảnh báo thông minh và phát triển hệ thống Core Web API trên ASP.NET Core theo quy trình chuẩn linh hoạt (Flexible Flow) phù hợp cho mọi nhà máy sử dụng **PostgreSQL** làm cơ sở dữ liệu duy nhất.

---

## 🔍 1. REINDEX & PHÂN TÍCH CHUYÊN SÂU CÁC MODULES CŨ

### 📥 Phân hệ Nhập kho (Kế thừa từ quy trình Nhập kho cũ)
* **Hành vi cũ**: 
  * Xác định chế độ nhập (Normal vs Wafer) bằng cách so sánh chuỗi tiền tố sản phẩm (hardcode).
  * Gọi danh sách Invoice để chọn PO/Invoice. Kiểm tra chênh lệch số lượng còn lại ở Client.
  * Tồn tại hàm vá lỗi khẩn cấp để kiểm tra chênh lệch thời gian và cache dữ liệu nhằm tránh nhập trùng lặp khi nhiều máy trạm cùng ấn lưu một lúc.
* **Giải pháp chuẩn hóa Flexible**:
  * Đưa cấu hình phân loại hàng (`IsWafer`, `IqcCheckType`) về PostgreSQL cấu hình theo sản phẩm.
  * Việc kiểm tra chênh lệch số lượng và trùng lặp hóa đơn được xử lý triệt để ở **Database Transaction Level** của Backend thông qua cơ chế khóa (Pessimistic Locking hoặc Serialized Transaction) để ngăn chặn tuyệt đối tình trạng race condition khi nhiều máy quét đồng thời lưu.

### 🔬 Phân hệ Kiểm QC (Kế thừa từ quy trình QC cũ)
* **Hành vi cũ**:
  * Các Lot sau khi nhập sẽ rơi vào trạng thái kiểm tra chất lượng mặc định. Trực tiếp thay đổi flag trên bảng quản lý nhóm hóa đơn cũ.
* **Giải pháp chuẩn hóa Flexible**:
  * Cho phép thiết lập bật/tắt yêu cầu kiểm tra chất lượng cho từng loại hàng hoặc đối tác cụ thể qua cấu hình `IqcCheckType` (`FULL`, `SAMPLE`, `NONE`). Nếu cấu hình là `NONE`, khi nhập kho thành công, Lot sẽ tự động được gán flag sẵn sàng sử dụng (`STATUS = 'READY'`) mà không cần qua bước duyệt QC.

### ✂️ Phân hệ Chia nhỏ Lot (Kế thừa từ quy trình Kowake cũ)
* **Bug lịch sử đã phát hiện**:
  * Trong logic cũ, khi tạo danh sách Inner Lot, ô nhập liệu cột `Maker Inner Parts LotNo` bị set bằng chuỗi rỗng (`""`), trong khi dữ liệu thật chỉ được đẩy lên một Label hiển thị bên ngoài chứ không cập nhật vào lưới dữ liệu. Dẫn đến tình trạng khi chế độ kiểm soát của nhà cung cấp khóa sửa đổi, người dùng nhìn thấy dữ liệu trên label nhưng ô lưới lại trống trơn và không thể lưu.
* **Giải pháp sửa đổi & Chuẩn hóa**:
  * Trên API Backend, thiết kế luồng tự động điền `MakerInnerLotNo` từ Lot cha nếu cấu hình của sản phẩm cho phép thừa kế mã Lot nhà sản xuất. Giao diện Web SPA sẽ hiển thị chính xác giá trị thừa kế này trên bảng dữ liệu để người dùng kiểm tra trước khi xác nhận.

### 📤 Phân hệ Xuất kho & Kiểm FIFO (Kế thừa từ quy trình Xuất kho cũ)
* **Hành vi cũ**:
  * Kiểm tra FIFO bằng cách so sánh ngày sản xuất của các Lot đang lưu trong kho cũ. Logic kiểm tra cứng nhắc, dễ gây tắc nghẽn dây chuyền nếu nhà máy muốn xuất lô hàng mới trước vì lý do đặc biệt (Ví dụ: Yêu cầu khẩn cấp từ khách hàng).
* **Giải pháp chuẩn hóa Flexible**:
  * Bổ sung trường cấu hình `FifoPolicyLevel` (0: Tắt kiểm tra, 1: Cảnh báo nhưng cho phép ghi đè bằng mã OTP/Quyền Admin, 2: Chặn cứng).
  * API kiểm tra FIFO sẽ trả về mã trạng thái chi tiết. Frontend căn cứ vào cấu hình để hiển thị hộp thoại yêu cầu quyền phê duyệt (Bypass) của Quản lý nếu nhà máy muốn xuất phá quy trình FIFO.

---

## 🔐 2. HỆ THỐNG XÁC THỰC & PHÂN QUYỀN TRÊN BACKEND (JWT & RBAC AUTHORIZATION)

Hệ thống sử dụng cơ chế bảo mật JWT Bearer Token kết hợp phân quyền dựa trên mã quyền chi tiết (Claim-Based Authorization).

### A. Custom Authorization Attribute: `HasPermissionAttribute.cs`
Thay vì phân quyền theo Role tĩnh dễ bị giới hạn, backend sử dụng hệ thống lọc quyền chi tiết (Permissions):
```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    public string Permission { get; }

    public HasPermissionAttribute(string permission)
    {
        Permission = permission;
    }
}
```

---

## 🛠️ 3. PHÁT TRIỂN CORE API CONTROLLER VỚI RÀNG BUỘC PHÂN QUYỀN

### A. Controller Tiếp nhận Vật tư & Gợi ý Slotting: `PartInputController.cs`
Yêu cầu người dùng đăng nhập và có quyền `material.accept`, tích hợp thuật toán Slotting cất hàng tối ưu và đề xuất Cross-Docking tự động:
```csharp
[ApiController]
[Route("api/part-input")]
[Authorize]
public class PartInputController : ControllerBase
{
    private readonly IPartInputService _inputService;
    private readonly ISlottingService _slottingService;

    public PartInputController(IPartInputService inputService, ISlottingService slottingService)
    {
        _inputService = inputService;
        _slottingService = slottingService;
    }

    [HttpPost("accept")]
    [HasPermission("material.accept")]
    public async Task<IActionResult> AcceptMaterial([FromBody] MaterialAcceptRequest request)
    {
        var validationResult = await _inputService.ValidateRequestAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ErrorMessage);
        }

        try
        {
            var lotInfo = await _inputService.ProcessAcceptanceAsync(request);
            var proposedLocations = await _slottingService.GetPutawayProposalsAsync(lotInfo.Id, request.TenantId);
            var crossDockingMatched = await _inputService.CheckCrossDockingMatchAsync(lotInfo.ProductId, lotInfo.OriginalQty);

            return Ok(new { 
                Message = "Tiếp nhận vật tư thành công", 
                LotNo = lotInfo.LotNo,
                ProposedLocations = proposedLocations,
                CrossDockProposal = crossDockingMatched
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
```

### B. Controller Đợt Gom Hàng Xuất: `WavePickingController.cs`
Gom nhiều yêu cầu xuất hàng lẻ thành các đợt (Wave) để tối ưu hóa quãng đường di chuyển lấy hàng:
```csharp
[ApiController]
[Route("api/wave-picking")]
[Authorize]
public class WavePickingController : ControllerBase
{
    private readonly IWavePickingService _waveService;

    public WavePickingController(IWavePickingService waveService)
    {
        _waveService = waveService;
    }

    [HttpPost("create")]
    [HasPermission("wave.manage")]
    public async Task<IActionResult> CreateWave([FromBody] CreateWaveDto dto)
    {
        var waveId = await _waveService.CreatePickingWaveAsync(dto);
        return Ok(new { Message = "Tạo đợt gom hàng xuất thành công", WaveId = waveId });
    }

    [HttpGet("{id}/pick-list")]
    [HasPermission("shipment.manage")]
    public async Task<IActionResult> GetOptimizedPickList(Guid id)
    {
        // Trích xuất danh sách lấy hàng tối ưu: nhóm cùng mặt hàng, cùng vị trí kệ
        var pickList = await _waveService.GetOptimizedPickListAsync(id);
        return Ok(pickList);
    }
}
```

### C. Controller Gia Phả Vật Tư: `LotTraceabilityController.cs`
API truy xuất gia phả Lot cha $\rightarrow$ Lot con (Kowake Tree) phục vụ kiểm toán chất lượng và khoanh vùng sự cố:
```csharp
[ApiController]
[Route("api/lot-traceability")]
[Authorize]
public class LotTraceabilityController : ControllerBase
{
    private readonly ILotTraceabilityService _traceService;

    public LotTraceabilityController(ILotTraceabilityService traceService)
    {
        _traceService = traceService;
    }

    [HttpGet("{lotNo}/genealogy")]
    public async Task<IActionResult> GetLotGenealogy(string lotNo)
    {
        // Trả về cấu trúc cây phân cấp (Hierarchical Tree) của Lot từ Lot cha gốc tới các Lot con kowake
        var genealogyTree = await _traceService.GetLotGenealogyTreeAsync(lotNo);
        if (genealogyTree == null)
        {
            return NotFound("Không tìm thấy thông tin Lot hàng");
        }
        return Ok(genealogyTree);
    }
}
```

### D. Controller Đo lường Năng suất Lao động: `LaborController.cs`
```csharp
[ApiController]
[Route("api/labor")]
[Authorize]
public class LaborController : ControllerBase
{
    private readonly ILaborTrackingService _laborService;

    public LaborController(ILaborTrackingService laborService)
    {
        _laborService = laborService;
    }

    [HttpPost("start-task")]
    public async Task<IActionResult> StartTask([FromBody] StartTaskDto dto)
    {
        var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var taskId = await _laborService.StartLaborTaskAsync(currentUserId, dto.TaskType, dto.ReferenceId);
        return Ok(new { TaskId = taskId });
    }

    [HttpPost("end-task/{id}")]
    public async Task<IActionResult> EndTask(Guid id)
    {
        var success = await _laborService.CompleteLaborTaskAsync(id);
        if (!success)
        {
            return BadRequest("Không thể hoàn thành tác vụ.");
        }
        return Ok("Ghi nhận hoàn thành tác vụ thành công.");
    }
}
```

---

## 🔒 4. CƠ CHẾ BẢO MẬT MULTI-TENANT (TENANT ISOLATION)
Để đảm bảo dữ liệu của các nhà máy không bị rò rỉ lẫn nhau, chúng ta cấu hình cơ chế Global Query Filter trong EF Core `NexustockDbContext.cs`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Warehouse>().HasQueryFilter(w => w.TenantId == _currentTenantService.TenantId);
    modelBuilder.Entity<StockImport>().HasQueryFilter(i => i.TenantId == _currentTenantService.TenantId);
    modelBuilder.Entity<StockExport>().HasQueryFilter(e => e.TenantId == _currentTenantService.TenantId);
}
```
