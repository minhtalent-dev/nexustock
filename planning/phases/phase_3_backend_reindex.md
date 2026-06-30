# PHASE 3: REINDEX MODULES CŨ & PHÁT TRIỂN CORE API BACKEND (REINDEX & BACKEND SERVICES)

Phase này phân tích chuyên sâu (Reindex) mã nguồn của các module nghiệp vụ trong các dự án cũ để sàng lọc, loại bỏ logic hardcode, khắc phục các bug lịch sử, tích hợp hệ thống phân quyền tài khoản chi tiết, quy trình kiểm kê định kỳ, phân hoạch vùng kho bảo quản, thuật toán cất hàng tối ưu (Putaway Slotting), quy trình chuyển tiếp trực tiếp (Cross-Docking), gom đơn hàng xuất kho (Wave Picking), cây gia phả truy vết chất lượng (Material Genealogy), đo lường năng suất lao động (Labor Tracking), lịch hẹn cửa kho (Dock Scheduling), định danh Pallet/Container (LPN), quản lý Số Serial riêng lẻ, quy trình hàng trả về (RMA), thuật toán đan xen tác vụ (Task Interleaving), cấu hình cảnh báo thông minh và phát triển hệ thống Core Web API trên ASP.NET Core theo quy trình chuẩn linh hoạt (Flexible Flow) phù hợp cho mọi nhà máy sử dụng **PostgreSQL** làm cơ sở dữ liệu duy nhất.

---

## 🔍 1. REINDEX & PHÂN TÍCH CHUYÊN SÂU C CÁC MODULES CŨ

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

### B. Controller Quản lý Pallet (LPN): `LicensePlateController.cs`
Gộp và di chuyển toàn bộ Lot hàng trên cùng một LPN chỉ bằng 1 lần quét:
```csharp
[ApiController]
[Route("api/lpn")]
[Authorize]
public class LicensePlateController : ControllerBase
{
    private readonly ILpnService _lpnService;

    public LicensePlateController(ILpnService lpnService)
    {
        _lpnService = lpnService;
    }

    [HttpPost("consolidate")]
    [HasPermission("lpn.manage")]
    public async Task<IActionResult> ConsolidateLotsToPallet([FromBody] ConsolidateLpnDto dto)
    {
        var success = await _lpnService.ConsolidateLotsAsync(dto.LpnCode, dto.LotIds, dto.TenantId);
        if (!success)
        {
            return BadRequest("Gộp Lot vào Pallet thất bại.");
        }
        return Ok("Đóng gói Pallet (LPN) thành công.");
    }

    [HttpPost("move")]
    [HasPermission("stock.move")]
    public async Task<IActionResult> MovePalletLocation([FromBody] MoveLpnDto dto)
    {
        // Di chuyển toàn bộ các Lot nằm trên Pallet sang vị trí mới qua 1 Transaction
        var success = await _lpnService.MovePalletAsync(dto.LpnCode, dto.TargetLocationId);
        if (!success)
        {
            return BadRequest("Di chuyển Pallet thất bại.");
        }
        return Ok("Di chuyển Pallet thành công.");
    }
}
```

### C. Controller Điều phối Đan xen Tác vụ: `TaskInterleavingService.cs`
Tự động gán tác vụ gần vị trí kệ khi vừa cất hàng để giảm thiểu quãng đường chạy xe không:
```csharp
public class TaskInterleavingService : ITaskInterleavingService
{
    private readonly NexustockDbContext _context;

    public TaskInterleavingService(NexustockDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> GetNextInterleavedTaskAsync(Guid userId, Guid completedLocationId, Guid tenantId)
    {
        // 1. Tìm vị trí kệ vừa hoàn thành
        var currentLocation = await _context.StorageLocations.FindAsync(completedLocationId);
        if (currentLocation == null) return null;

        // 2. Tìm tác vụ lấy hàng xuất (PICK) đang chờ trong Task Queue
        var pendingTask = await _context.LaborTasks
            .Where(t => t.TenantId == tenantId && t.Status == "PENDING" && t.TaskType == "PICK")
            .Join(_context.ShipmentItems, t => t.ReferenceId, s => s.Id, (t, s) => new { Task = t, ShipmentItem = s })
            .Join(_context.Inventories, s => s.ShipmentItem.LotId, i => i.LotId, (s, i) => new { s.Task, Inventory = i })
            .Where(x => x.Inventory.Location.WarehouseId == currentLocation.WarehouseId) // Cùng nhà kho
            .OrderBy(x => x.Inventory.Location.Code) // Sắp xếp theo mã kệ để tìm vị trí gần nhất
            .FirstOrDefaultAsync();

        if (pendingTask != null)
        {
            // Tự động gán tác vụ này cho công nhân hiện tại
            pendingTask.Task.UserId = userId;
            pendingTask.Task.Status = "IN_PROGRESS";
            pendingTask.Task.StartTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return pendingTask.Task.Id;
        }

        return null;
    }
}
```

### D. Controller Khách hàng Trả hàng (RMA): `RmaController.cs`
```csharp
[ApiController]
[Route("api/rma")]
[Authorize]
public class RmaController : ControllerBase
{
    private readonly IRmaService _rmaService;

    public RmaController(IRmaService rmaService)
    {
        _rmaService = rmaService;
    }

    [HttpPost("receive")]
    [HasPermission("rma.manage")]
    public async Task<IActionResult> ReceiveRmaItem([FromBody] ReceiveRmaDto dto)
    {
        var result = await _rmaService.ProcessRmaReceiptAsync(dto);
        return Ok(result);
    }

    [HttpPost("judge")]
    [HasPermission("material.hold")]
    public async Task<IActionResult> JudgeRmaItem([FromBody] JudgeRmaDto dto)
    {
        // Thực hiện phân loại: Tái nhập kho (RESTOCK), Sửa chữa (REWORK), Hủy bỏ (SCRAP)
        var success = await _rmaService.JudgeAndRouteItemAsync(dto.RmaItemId, dto.Judgement, dto.TargetLocationId);
        if (!success)
        {
            return BadRequest("Cập nhật quyết định QC thất bại.");
        }
        return Ok("Cập nhật quyết định QC thành công.");
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
