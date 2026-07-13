using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Dtos;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Exceptions.Contexts;

namespace Nexustock.Modules.Inventory.Controllers;

[Authorize]
[ApiController]
[Route("api/mobile")]
public class MobileController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly MasterDataDbContext _masterContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ExceptionsDbContext? _exceptionsContext;

    public MobileController(
        InventoryDbContext context,
        MasterDataDbContext masterContext,
        ITenantProvider tenantProvider,
        ExceptionsDbContext? exceptionsContext = null)
    {
        _context = context;
        _masterContext = masterContext;
        _tenantProvider = tenantProvider;
        _exceptionsContext = exceptionsContext;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    [HttpPost("scan/validate")]
    public async Task<IActionResult> ValidateBarcode([FromBody] ScanValidateRequestDto dto)
    {
        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";
        var stopwatch = Stopwatch.StartNew();
        string result = "VALID";

        try
        {
            if (dto.Context == "LOCATION")
            {
                var loc = await _masterContext.StorageLocations
                    .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Code == dto.Barcode);
                
                if (loc == null)
                {
                    result = "INVALID_LOCATION_NOT_FOUND";
                    throw new Nexustock.Modules.Exceptions.Entities.OperationalBusinessException(
                        "Vị trí không tồn tại",
                        result,
                        "LOW",
                        "BARCODE_SCAN",
                        Guid.Empty
                    );
                }

                // Check location lock
                var isLocked = await _context.LocationLocks
                    .AnyAsync(l => l.TenantId == tenantId && l.LocationId == loc.Id);
                if (isLocked)
                {
                    result = "INVALID_LOCATION_LOCKED";
                    throw new Nexustock.Modules.Exceptions.Entities.OperationalBusinessException(
                        "Vị trí đang bị phong tỏa kiểm kê",
                        result,
                        "MEDIUM",
                        "LOCATION",
                        loc.Id
                    );
                }
            }
            else if (dto.Context == "LOT")
            {
                var lotExists = await _context.Inventories
                    .AnyAsync(i => i.TenantId == tenantId && i.LotNo == dto.Barcode);
                if (!lotExists)
                {
                    result = "INVALID_LOT_NOT_FOUND";
                    throw new Nexustock.Modules.Exceptions.Entities.OperationalBusinessException(
                        "Không tìm thấy số lô hàng tồn kho",
                        result,
                        "LOW",
                        "LOT",
                        Guid.Empty,
                        lotNo: dto.Barcode
                    );
                }
            }
            else if (dto.Context == "ITEM")
            {
                var itemExists = await _masterContext.Products
                    .AnyAsync(p => p.TenantId == tenantId && p.Code == dto.Barcode);
                if (!itemExists)
                {
                    result = "INVALID_ITEM_NOT_FOUND";
                    throw new Nexustock.Modules.Exceptions.Entities.OperationalBusinessException(
                        "Sản phẩm không tồn tại trên hệ thống",
                        result,
                        "LOW",
                        "ITEM",
                        Guid.Empty
                    );
                }
            }
            else
            {
                result = "INVALID_CONTEXT";
                return BadRequest(new { errorCode = result, message = "Ngữ cảnh quét không hợp lệ" });
            }

            return Ok(new { message = "Mã vạch hợp lệ", barcode = dto.Barcode });
        }
        finally
        {
            stopwatch.Stop();
            var scanEvent = new ScanEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Context = dto.Context,
                Barcode = dto.Barcode,
                Result = result,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.ScanEvents.Add(scanEvent);
            await _context.SaveChangesAsync();
        }
    }

    [HttpPost("offline-sync")]
    public async Task<IActionResult> SyncOffline([FromBody] OfflineSyncRequestDto dto)
    {
        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var results = new List<object>();

            foreach (var opDto in dto.Operations)
            {
                var alreadySynced = await _context.OfflineOperations
                    .AnyAsync(o => o.TenantId == tenantId && o.ClientOperationId == opDto.ClientOperationId);
                if (alreadySynced)
                {
                    results.Add(new { opDto.ClientOperationId, status = "AlreadySynced" });
                    continue;
                }

                var offlineOp = new OfflineOperation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ClientOperationId = opDto.ClientOperationId,
                    Payload = opDto.Payload,
                    SyncStatus = "Synced",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };

                try
                {
                    if (opDto.StepType == "MOVE")
                    {
                        var moveData = JsonSerializer.Deserialize<MovePayload>(opDto.Payload);
                        if (moveData != null)
                        {
                            var inventory = await _context.Inventories.FirstOrDefaultAsync(i =>
                                i.TenantId == tenantId &&
                                i.ItemId == moveData.ItemId &&
                                i.LotNo == moveData.LotNo &&
                                i.LocationId == moveData.FromLocationId);

                            if (inventory == null)
                            {
                                throw new Exception("Không tìm thấy số dư tồn kho nguồn để dịch chuyển");
                            }

                            if (inventory.QtyOnHand < moveData.Qty)
                            {
                                throw new Exception("Số lượng tồn kho nguồn không đủ để dịch chuyển");
                            }

                            // 1. Trừ tồn kho nguồn
                            inventory.QtyOnHand -= moveData.Qty;
                            inventory.UpdatedAt = DateTime.UtcNow;
                            inventory.UpdatedBy = username;

                            if (inventory.QtyOnHand == 0 && inventory.QtyReserved == 0)
                            {
                                _context.Inventories.Remove(inventory);
                            }

                            // 2. Cộng tồn kho đích
                            var destInventory = await _context.Inventories.FirstOrDefaultAsync(i =>
                                i.TenantId == tenantId &&
                                i.ItemId == moveData.ItemId &&
                                i.LotNo == moveData.LotNo &&
                                i.LocationId == moveData.ToLocationId);

                            if (destInventory == null)
                            {
                                destInventory = new Entities.Inventory
                                {
                                    Id = Guid.NewGuid(),
                                    TenantId = tenantId,
                                    ItemId = moveData.ItemId,
                                    LotNo = moveData.LotNo,
                                    LocationId = moveData.ToLocationId,
                                    QtyOnHand = moveData.Qty,
                                    QtyReserved = 0,
                                    CreatedAt = DateTime.UtcNow,
                                    CreatedBy = username
                                };
                                _context.Inventories.Add(destInventory);
                            }
                            else
                            {
                                destInventory.QtyOnHand += moveData.Qty;
                                destInventory.UpdatedAt = DateTime.UtcNow;
                                destInventory.UpdatedBy = username;
                            }

                            // 3. Ghi ledger transactions
                            var traceId = HttpContext.TraceIdentifier;
                            var outTransaction = new InventoryTransaction
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                ItemId = moveData.ItemId,
                                LotNo = moveData.LotNo,
                                LocationId = moveData.FromLocationId,
                                TransactionType = "MOVE_OUT",
                                Qty = -moveData.Qty,
                                TraceId = traceId,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = username
                            };
                            _context.InventoryTransactions.Add(outTransaction);

                            var inTransaction = new InventoryTransaction
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                ItemId = moveData.ItemId,
                                LotNo = moveData.LotNo,
                                LocationId = moveData.ToLocationId,
                                TransactionType = "MOVE_IN",
                                Qty = moveData.Qty,
                                TraceId = traceId,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = username
                            };
                            _context.InventoryTransactions.Add(inTransaction);
                        }
                    }
                }
                catch (Exception ex)
                {
                    offlineOp.SyncStatus = "Failed";
                    offlineOp.ErrorMessage = ex.Message;

                    if (_exceptionsContext != null)
                    {
                        try
                        {
                            var dateStr = DateTime.UtcNow.ToString("yyMMdd");
                            var countToday = await _exceptionsContext.OperationalExceptions
                                .IgnoreQueryFilters()
                                .CountAsync(e => e.TenantId == tenantId && e.Code.StartsWith($"EX-{dateStr}-"));
                            var exceptionCode = $"EX-{dateStr}-{(countToday + 1):D4}";

                            Guid itemId = Guid.Empty;
                            string? lotNo = null;
                            Guid? locationId = null;
                            decimal qty = 0;
                            try
                            {
                                var moveData = JsonSerializer.Deserialize<MovePayload>(opDto.Payload);
                                if (moveData != null)
                                {
                                    itemId = moveData.ItemId;
                                    lotNo = moveData.LotNo;
                                    locationId = moveData.FromLocationId;
                                    qty = moveData.Qty;
                                }
                            }
                            catch {}

                            var opException = new Nexustock.Modules.Exceptions.Entities.OperationalException
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                Code = exceptionCode,
                                Type = "OFFLINE_SYNC_FAILED",
                                Severity = "HIGH",
                                Status = "Open",
                                ReferenceType = "OFFLINE_OP",
                                ReferenceId = Guid.TryParse(opDto.ClientOperationId, out var gId) ? gId : Guid.Empty,
                                LocationId = locationId,
                                LotNo = lotNo,
                                Qty = qty,
                                ReasonCode = "SYNC_CONFLICT",
                                Note = $"Dong bo offline that bai: {ex.Message}",
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = username
                            };
                            _exceptionsContext.OperationalExceptions.Add(opException);

                            var @event = new Nexustock.Modules.Exceptions.Entities.ExceptionEvent
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                ExceptionId = opException.Id,
                                Transition = "CREATE_AUTO",
                                Actor = username,
                                Note = $"Tu dong ghi nhan tu offline sync fail. ClientOpId: {opDto.ClientOperationId}",
                                CreatedAt = DateTime.UtcNow
                            };
                            _exceptionsContext.ExceptionEvents.Add(@event);

                            await _exceptionsContext.SaveChangesAsync();
                        }
                        catch (Exception ex2)
                        {
                            // Thay vi dung Serilog.Log directly, ta co thể dùng System.Console.WriteLine hoặc System.Diagnostics.Debug
                            System.Console.WriteLine($"Loi khi tu dong tao OperationalException tu Offline Sync: {ex2.Message}");
                        }
                    }
                }

                _context.OfflineOperations.Add(offlineOp);
                results.Add(new { opDto.ClientOperationId, status = offlineOp.SyncStatus, error = offlineOp.ErrorMessage });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Đồng bộ thành công", results });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { errorCode = "SYNC_FAILED", message = ex.Message });
        }
    }

    [HttpGet("tasks/next")]
    public async Task<IActionResult> GetNextTask([FromQuery] string? currentLocationCode)
    {
        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var openTasks = await _context.MobileTasks
            .Where(t => t.TenantId == tenantId && t.Status == "Open")
            .ToListAsync();

        if (!openTasks.Any())
        {
            return Ok(new { errorCode = "NO_TASKS_AVAILABLE", message = "Không còn nhiệm vụ nào trong bể công việc." });
        }

        MobileTask? selectedTask = null;

        if (!string.IsNullOrEmpty(currentLocationCode))
        {
            var currentLoc = await _masterContext.StorageLocations
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Code == currentLocationCode);

            if (currentLoc != null)
            {
                var zoneLocations = await _masterContext.StorageLocations
                    .Where(l => l.TenantId == tenantId && l.ZoneId == currentLoc.ZoneId)
                    .OrderBy(l => l.Code)
                    .ToListAsync();

                var targetLocationIds = openTasks.Where(t => t.LocationId.HasValue).Select(t => t.LocationId!.Value).ToList();

                var sortedLocations = zoneLocations
                    .Where(l => targetLocationIds.Contains(l.Id))
                    .OrderBy(l => Math.Abs(zoneLocations.IndexOf(l) - zoneLocations.IndexOf(currentLoc)))
                    .ToList();

                if (sortedLocations.Any())
                {
                    var nearestLocId = sortedLocations.First().Id;
                    selectedTask = openTasks.FirstOrDefault(t => t.LocationId == nearestLocId);
                }
            }
        }

        if (selectedTask == null)
        {
            selectedTask = openTasks.OrderBy(t => t.CreatedAt).First();
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var dbTask = await _context.MobileTasks.FirstOrDefaultAsync(t => t.Id == selectedTask.Id);
            if (dbTask == null || dbTask.Status != "Open")
            {
                return BadRequest(new { errorCode = "TASK_ALREADY_CLAIMED", message = "Nhiệm vụ đã được nhận bởi người khác. Vui lòng tải lại." });
            }

            dbTask.Status = "In_Progress";
            dbTask.AssignedUser = username;
            dbTask.UpdatedAt = DateTime.UtcNow;
            dbTask.UpdatedBy = username;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { 
                task = new {
                    dbTask.Id,
                    dbTask.ReferenceType,
                    dbTask.ReferenceId,
                    dbTask.Step,
                    dbTask.LocationId,
                    dbTask.AssignedUser,
                    dbTask.Status
                },
                message = "Đã nhận việc thành công"
            });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("tasks/{id:guid}/complete")]
    public async Task<IActionResult> CompleteTask(Guid id)
    {
        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var dbTask = await _context.MobileTasks
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id);

            if (dbTask == null)
            {
                return NotFound(new { errorCode = "TASK_NOT_FOUND", message = "Không tìm thấy nhiệm vụ." });
            }

            if (dbTask.Status == "Completed")
            {
                return BadRequest(new { errorCode = "TASK_ALREADY_COMPLETED", message = "Nhiệm vụ đã hoàn thành trước đó." });
            }

            dbTask.Status = "Completed";
            dbTask.UpdatedAt = DateTime.UtcNow;
            dbTask.UpdatedBy = username;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Đã hoàn thành nhiệm vụ thành công" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { errorCode = "TASK_COMPLETE_FAILED", message = ex.Message });
        }
    }

    private class MovePayload
    {
        public Guid ItemId { get; set; }
        public string LotNo { get; set; } = null!;
        public Guid FromLocationId { get; set; }
        public Guid ToLocationId { get; set; }
        public decimal Qty { get; set; }
    }
}
