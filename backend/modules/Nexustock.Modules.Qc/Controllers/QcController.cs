using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Qc.Contexts;
using Nexustock.Modules.Qc.Dtos;
using Nexustock.Modules.Qc.Entities;
using QcTenantProvider = Nexustock.Modules.Qc.Services.ITenantProvider;

namespace Nexustock.Modules.Qc.Controllers;

[Authorize]
[ApiController]
[Route("api/qc")]
public class QcController : ControllerBase
{
    private readonly QcDbContext _qcContext;
    private readonly InboundDbContext _inboundContext;
    private readonly MasterDataDbContext _masterContext;
    private readonly QcTenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;

    public QcController(
        QcDbContext qcContext,
        InboundDbContext inboundContext,
        MasterDataDbContext masterContext,
        QcTenantProvider tenantProvider,
        IUserPermissionService permissionService)
    {
        _qcContext = qcContext;
        _inboundContext = inboundContext;
        _masterContext = masterContext;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;

        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue()
    {
        if (!await HasPermissionAsync("Qc.Queue.View"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();

        // 1. Lấy tất cả Lot có QcStatus = Unspec thuộc Tenant
        var unspecLots = await _inboundContext.Lots
            .Where(l => l.QcStatus == LotQcStatus.Unspec && l.TenantId == tenantId)
            .ToListAsync();

        // 2. Lấy tất cả QcRequest có Status = Pending thuộc Tenant
        var pendingRequests = await _qcContext.QcRequests
            .Where(r => r.Status == QcRequestStatus.Pending && r.TenantId == tenantId)
            .ToListAsync();

        // 3. Tự động đồng bộ tạo QcRequest cho các Lot Unspec chưa có Request
        var existingLotIds = pendingRequests.Select(r => r.LotId).ToHashSet();
        var newRequests = new List<QcRequest>();

        foreach (var lot in unspecLots)
        {
            if (!existingLotIds.Contains(lot.Id))
            {
                var hasRequest = await _qcContext.QcRequests.AnyAsync(r => r.LotId == lot.Id && r.TenantId == tenantId);
                if (!hasRequest)
                {
                    var req = new QcRequest
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        LotId = lot.Id,
                        SamplePlan = "Standard QC Plan",
                        Status = QcRequestStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = User.Identity?.Name ?? "System"
                    };
                    newRequests.Add(req);
                }
            }
        }

        if (newRequests.Count > 0)
        {
            _qcContext.QcRequests.AddRange(newRequests);
            await _qcContext.SaveChangesAsync();
            pendingRequests.AddRange(newRequests);
        }

        // 4. Lấy đầy đủ thông tin Lot, Product, và các thông số đo lường
        var lotIds = pendingRequests.Select(r => r.LotId).ToList();
        var lots = await _inboundContext.Lots
            .Where(l => lotIds.Contains(l.Id) && l.TenantId == tenantId)
            .ToListAsync();

        var itemIds = lots.Select(l => l.ItemId).Distinct().ToList();
        var products = await _masterContext.Products
            .Where(p => itemIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Code });

        // Tính receivedQty từ InventoryTransactions (RECEIVE)
        var lotNos = lots.Select(l => l.LotNo).ToList();
        var transactions = await _inboundContext.InventoryTransactions
            .Where(t => lotNos.Contains(t.LotNo) && itemIds.Contains(t.ItemId) && t.TransactionType == "RECEIVE" && t.TenantId == tenantId)
            .ToListAsync();

        var receivedQtys = transactions
            .GroupBy(t => new { t.LotNo, t.ItemId })
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Qty));

        // Lấy expectedQty từ InboundOrderItems (phiếu nhập mới nhất)
        var orderItems = await _inboundContext.InboundOrderItems
            .Include(i => i.InboundOrder)
            .Where(i => itemIds.Contains(i.ItemId) && i.TenantId == tenantId)
            .ToListAsync();

        var expectedQtys = orderItems
            .GroupBy(i => i.ItemId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(i => i.InboundOrder.CreatedAt).Select(i => i.ExpectedQty).FirstOrDefault()
            );

        var response = pendingRequests
            .Join(lots, r => r.LotId, l => l.Id, (r, l) => new { r, l })
            .Select(combined => {
                var prod = products.TryGetValue(combined.l.ItemId, out var p) ? p : null;
                var rKey = new { combined.l.LotNo, combined.l.ItemId };
                var recvQty = receivedQtys.TryGetValue(rKey, out var q) ? q : 0;
                var expQty = expectedQtys.TryGetValue(combined.l.ItemId, out var eq) ? eq : 0;

                return new QcQueueResponseDto
                {
                    Id = combined.r.Id,
                    LotId = combined.l.Id,
                    LotNo = combined.l.LotNo,
                    ItemId = combined.l.ItemId,
                    ItemName = prod?.Name ?? "Unknown Item",
                    ItemCode = prod?.Code ?? "Unknown Code",
                    ExpectedQty = expQty,
                    ReceivedQty = recvQty,
                    CreatedAt = combined.r.CreatedAt
                };
            })
            .OrderByDescending(dto => dto.CreatedAt)
            .ToList();

        return Ok(response);
    }

    [HttpPost("{lotId:guid}/result")]
    public async Task<IActionResult> RecordResult(Guid lotId, [FromBody] RecordQcResultDto dto)
    {
        if (!await HasPermissionAsync("Qc.Results.Create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var lot = await _inboundContext.Lots.FindAsync(lotId);
        
        if (lot == null) return NotFound("Lot not found");
        if (lot.TenantId != tenantId) return Forbid(); // Phòng chống IDOR

        var request = await _qcContext.QcRequests.FindAsync(dto.QcRequestId);
        if (request == null) return BadRequest("QC Request not found");
        if (request.TenantId != tenantId) return Forbid();
        if (request.Status != QcRequestStatus.Pending) return BadRequest("QC Request is not pending");

        if (_inboundContext.Database.IsRelational())
        {
            var connection = _inboundContext.Database.GetDbConnection();
            _qcContext.Database.SetDbConnection(connection);

            using var transaction = await _inboundContext.Database.BeginTransactionAsync();
            await _qcContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
            try
            {
                var result = new QcResult
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    QcRequestId = dto.QcRequestId,
                    IsPassed = dto.IsPassed,
                    Metrics = dto.Metrics,
                    AttachmentRefs = dto.AttachmentRefs,
                    Inspector = User.Identity?.Name ?? "System",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "System"
                };
                _qcContext.QcResults.Add(result);

                request.Status = QcRequestStatus.Completed;
                request.UpdatedAt = DateTime.UtcNow;
                request.UpdatedBy = User.Identity?.Name ?? "System";

                lot.QcStatus = dto.IsPassed ? LotQcStatus.Release : LotQcStatus.Reject;

                await _qcContext.SaveChangesAsync();
                await _inboundContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return Conflict("Optimistic Concurrency exception: record was modified by another user.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            var result = new QcResult
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                QcRequestId = dto.QcRequestId,
                IsPassed = dto.IsPassed,
                Metrics = dto.Metrics,
                AttachmentRefs = dto.AttachmentRefs,
                Inspector = User.Identity?.Name ?? "System",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System"
            };
            _qcContext.QcResults.Add(result);
            request.Status = QcRequestStatus.Completed;
            lot.QcStatus = dto.IsPassed ? LotQcStatus.Release : LotQcStatus.Reject;
            await _qcContext.SaveChangesAsync();
            await _inboundContext.SaveChangesAsync();
        }

        return Ok(new { message = "Recorded QC result successfully" });
    }

    [HttpPost("{lotId:guid}/hold")]
    public async Task<IActionResult> ActiveHold(Guid lotId, [FromBody] HoldLotDto dto)
    {
        if (!await HasPermissionAsync("Qc.Lots.Hold"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var lot = await _inboundContext.Lots.FindAsync(lotId);
        
        if (lot == null) return NotFound("Lot not found");
        if (lot.TenantId != tenantId) return Forbid(); // Phòng chống IDOR

        if (_inboundContext.Database.IsRelational())
        {
            var connection = _inboundContext.Database.GetDbConnection();
            _qcContext.Database.SetDbConnection(connection);

            using var transaction = await _inboundContext.Database.BeginTransactionAsync();
            await _qcContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
            try
            {
                var hold = new MaterialHold
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LotId = lot.Id,
                    LocationId = dto.LocationId,
                    ReasonCode = dto.ReasonCode,
                    Status = "Active",
                    HeldBy = User.Identity?.Name ?? "System",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "System"
                };
                _qcContext.MaterialHolds.Add(hold);

                lot.QcStatus = LotQcStatus.Hold;

                await _qcContext.SaveChangesAsync();
                await _inboundContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return Conflict("Optimistic Concurrency exception: record was modified by another user.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            var hold = new MaterialHold
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LotId = lot.Id,
                LocationId = dto.LocationId,
                ReasonCode = dto.ReasonCode,
                Status = "Active",
                HeldBy = User.Identity?.Name ?? "System",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System"
            };
            _qcContext.MaterialHolds.Add(hold);
            lot.QcStatus = LotQcStatus.Hold;
            await _qcContext.SaveChangesAsync();
            await _inboundContext.SaveChangesAsync();
        }

        return Ok(new { message = "Lot held successfully" });
    }

    [HttpPost("{lotId:guid}/release")]
    public async Task<IActionResult> ReleaseHold(Guid lotId, [FromBody] ReleaseLotDto dto)
    {
        if (!await HasPermissionAsync("Qc.Lots.Release"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var lot = await _inboundContext.Lots.FindAsync(lotId);
        
        if (lot == null) return NotFound("Lot not found");
        if (lot.TenantId != tenantId) return Forbid(); // Phòng chống IDOR

        if (_inboundContext.Database.IsRelational())
        {
            var connection = _inboundContext.Database.GetDbConnection();
            _qcContext.Database.SetDbConnection(connection);

            using var transaction = await _inboundContext.Database.BeginTransactionAsync();
            await _qcContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
            try
            {
                var activeHolds = await _qcContext.MaterialHolds
                    .Where(h => h.LotId == lot.Id && h.Status == "Active" && h.TenantId == tenantId)
                    .ToListAsync();

                foreach (var hold in activeHolds)
                {
                    hold.Status = "Released";
                    hold.ReleasedBy = User.Identity?.Name ?? "System";
                    hold.ReleasedAt = DateTime.UtcNow;
                }

                lot.QcStatus = LotQcStatus.Release;

                await _qcContext.SaveChangesAsync();
                await _inboundContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return Conflict("Optimistic Concurrency exception: record was modified by another user.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            var activeHolds = await _qcContext.MaterialHolds
                .Where(h => h.LotId == lot.Id && h.Status == "Active" && h.TenantId == tenantId)
                .ToListAsync();

            foreach (var hold in activeHolds)
            {
                hold.Status = "Released";
                hold.ReleasedBy = User.Identity?.Name ?? "System";
                hold.ReleasedAt = DateTime.UtcNow;
            }

            lot.QcStatus = LotQcStatus.Release;
            await _qcContext.SaveChangesAsync();
            await _inboundContext.SaveChangesAsync();
        }

        return Ok(new { message = "Lot released successfully" });
    }

    [HttpPost("{lotId:guid}/reject")]
    public async Task<IActionResult> RejectLot(Guid lotId, [FromBody] RejectLotDto dto)
    {
        if (!await HasPermissionAsync("Qc.Lots.Reject"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var lot = await _inboundContext.Lots.FindAsync(lotId);
        
        if (lot == null) return NotFound("Lot not found");
        if (lot.TenantId != tenantId) return Forbid(); // Phòng chống IDOR

        if (_inboundContext.Database.IsRelational())
        {
            var connection = _inboundContext.Database.GetDbConnection();
            _qcContext.Database.SetDbConnection(connection);

            using var transaction = await _inboundContext.Database.BeginTransactionAsync();
            await _qcContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
            try
            {
                // Lưu vết lý do từ chối vào MaterialHolds dạng Rejected
                var hold = new MaterialHold
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LotId = lot.Id,
                    ReasonCode = dto.ReasonCode,
                    Status = "Active",
                    HeldBy = User.Identity?.Name ?? "System",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "System"
                };
                _qcContext.MaterialHolds.Add(hold);

                lot.QcStatus = LotQcStatus.Reject;

                await _qcContext.SaveChangesAsync();
                await _inboundContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return Conflict("Optimistic Concurrency exception: record was modified by another user.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            var hold = new MaterialHold
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LotId = lot.Id,
                ReasonCode = dto.ReasonCode,
                Status = "Active",
                HeldBy = User.Identity?.Name ?? "System",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System"
            };
            _qcContext.MaterialHolds.Add(hold);
            lot.QcStatus = LotQcStatus.Reject;
            await _qcContext.SaveChangesAsync();
            await _inboundContext.SaveChangesAsync();
        }

        return Ok(new { message = "Lot rejected successfully" });
    }
}
