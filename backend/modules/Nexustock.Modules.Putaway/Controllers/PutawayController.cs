using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Putaway.Contexts;
using Nexustock.Modules.Putaway.Dtos;
using Nexustock.Modules.Putaway.Entities;
using Nexustock.Modules.Putaway.Services;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.Qc.Abstractions;

namespace Nexustock.Modules.Putaway.Controllers;

[Authorize]
[ApiController]
[Route("api/putaway")]
public class PutawayController : ControllerBase
{
    private readonly PutawayDbContext _context;
    private readonly MasterDataDbContext _masterContext;
    private readonly InventoryDbContext _inventoryContext;
    private readonly IPutawayService _putawayService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IQcGateService _qcGate;

    public PutawayController(
        PutawayDbContext context,
        MasterDataDbContext masterContext,
        InventoryDbContext inventoryContext,
        IPutawayService putawayService,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IQcGateService qcGate)
    {
        _context = context;
        _masterContext = masterContext;
        _inventoryContext = inventoryContext;
        _putawayService = putawayService;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _qcGate = qcGate;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpGet("proposals")]
    public async Task<IActionResult> GetProposals([FromQuery] Guid lotId, [FromQuery] decimal qty)
    {
        if (!await HasPermissionAsync("putaway_slotting.read"))
        {
            return Forbid();
        }

        if (qty <= 0)
        {
            return BadRequest("Số lượng cất hàng phải lớn hơn 0");
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        try
        {
            var response = await _putawayService.GenerateProposalsAsync(tenantId, lotId, qty, username);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmPutaway([FromBody] ConfirmPutawayRequestDto dto)
    {
        if (!await HasPermissionAsync("putaway_slotting.create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";
        var traceId = HttpContext.TraceIdentifier;

        // 1. Fetch proposal and check status (Idempotency Guard)
        var proposal = await _context.PutawayProposals
            .FirstOrDefaultAsync(p => p.Id == dto.ProposalId && p.TenantId == tenantId);
        if (proposal == null)
        {
            return NotFound("Không tìm thấy đề xuất cất hàng");
        }

        if (proposal.Status == "CONFIRMED")
        {
            return Ok(new { success = true, message = "Đề xuất đã được xác nhận thành công trước đó (idempotent)." });
        }

        // 2. Fetch Lot
        var lot = await _inventoryContext.Lots
            .FirstOrDefaultAsync(l => l.Id == dto.LotId && l.TenantId == tenantId);
        if (lot == null)
        {
            return NotFound("Không tìm thấy lô hàng");
        }

        // 3. QC Gate — SoT Inbound.Lots
        try
        {
            await _qcGate.EnsureLotUsableAsync(tenantId, dto.LotId);
        }
        catch (QcGateException ex)
        {
            return StatusCode(ex.HttpStatus, new { errorCode = ex.ErrorCode, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }

        // 4. Verify Source & Destination location exist
        var sourceLoc = await _masterContext.StorageLocations
            .FirstOrDefaultAsync(l => l.Id == dto.FromLocationId && l.TenantId == tenantId);
        var targetLoc = await _masterContext.StorageLocations
            .FirstOrDefaultAsync(l => l.Id == dto.SelectedLocationId && l.TenantId == tenantId);
        if (sourceLoc == null || targetLoc == null)
        {
            return BadRequest(new { errorCode = "INVALID_LOCATION", message = "Vị trí nguồn hoặc vị trí đề xuất không hợp lệ" });
        }

        // 5. Verify locks
        var sourceLock = await _inventoryContext.LocationLocks
            .FirstOrDefaultAsync(l => l.LocationId == dto.FromLocationId && l.TenantId == tenantId);
        if (sourceLock != null && (sourceLock.LockType == "OUTBOUND" || sourceLock.LockType == "ALL"))
        {
            return BadRequest(new { errorCode = "LOCATION_LOCKED", message = "Vị trí nguồn đang bị khóa" });
        }

        var targetLock = await _inventoryContext.LocationLocks
            .FirstOrDefaultAsync(l => l.LocationId == dto.SelectedLocationId && l.TenantId == tenantId);
        if (targetLock != null && (targetLock.LockType == "INBOUND" || targetLock.LockType == "ALL"))
        {
            return BadRequest(new { errorCode = "LOCATION_LOCKED", message = "Vị trí đích đề xuất đang bị khóa" });
        }

        // 6. Verify Source Inventory
        var sourceInv = await _inventoryContext.Inventories
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == lot.ItemId && i.LotNo == lot.LotNo && i.LocationId == dto.FromLocationId);
        if (sourceInv == null || (sourceInv.QtyOnHand - sourceInv.QtyReserved) < dto.Qty)
        {
            return BadRequest(new { errorCode = "INSUFFICIENT_QTY", message = "Số lượng cất hàng vượt quá tồn khả dụng tại vị trí nguồn" });
        }

        // 7. Check Capacity Guard
        var currentQtyAtTarget = await _inventoryContext.Inventories
            .Where(i => i.LocationId == dto.SelectedLocationId && i.TenantId == tenantId)
            .SumAsync(i => i.QtyOnHand);

        if (currentQtyAtTarget + dto.Qty > targetLoc.MaxCapacity)
        {
            return BadRequest(new { errorCode = "LOCATION_OVER_CAPACITY", message = "Số lượng vượt quá sức chứa tối đa của vị trí kệ đề xuất" });
        }

        // 8. Execute shared transaction using TransactionScope
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // A. Update proposal status
            proposal.Status = "CONFIRMED";
            proposal.UpdatedAt = DateTime.UtcNow;
            proposal.UpdatedBy = username;

            // B. Deduct inventory from source
            sourceInv.QtyOnHand -= dto.Qty;
            sourceInv.UpdatedAt = DateTime.UtcNow;
            sourceInv.UpdatedBy = username;
            sourceInv.RowVersion += 1;

            if (sourceInv.QtyOnHand == 0 && sourceInv.QtyReserved == 0)
            {
                _inventoryContext.Inventories.Remove(sourceInv);
            }

            // C. Add inventory to target
            var targetInv = await _inventoryContext.Inventories
                .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == lot.ItemId && i.LotNo == lot.LotNo && i.LocationId == dto.SelectedLocationId);

            if (targetInv != null)
            {
                targetInv.QtyOnHand += dto.Qty;
                targetInv.UpdatedAt = DateTime.UtcNow;
                targetInv.UpdatedBy = username;
                targetInv.RowVersion += 1;
            }
            else
            {
                targetInv = new Nexustock.Modules.Inventory.Entities.Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = lot.ItemId,
                    LotNo = lot.LotNo,
                    LocationId = dto.SelectedLocationId,
                    QtyOnHand = dto.Qty,
                    QtyReserved = 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username,
                    RowVersion = 1
                };
                _inventoryContext.Inventories.Add(targetInv);
            }

            // D. Record movement log
            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = lot.ItemId,
                LotNo = lot.LotNo,
                FromLocationId = dto.FromLocationId,
                ToLocationId = dto.SelectedLocationId,
                Qty = dto.Qty,
                Status = "Completed",
                ReasonCode = "PUTAWAY_CONFIRM",
                TraceId = traceId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _inventoryContext.InventoryMovements.Add(movement);

            // E. Record Ledger transactions
            var transOut = new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = lot.ItemId,
                LotNo = lot.LotNo,
                LocationId = dto.FromLocationId,
                TransactionType = "MOVE_OUT",
                Qty = -dto.Qty,
                TraceId = traceId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _inventoryContext.InventoryTransactions.Add(transOut);

            var transIn = new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = lot.ItemId,
                LotNo = lot.LotNo,
                LocationId = dto.SelectedLocationId,
                TransactionType = "MOVE_IN",
                Qty = dto.Qty,
                TraceId = traceId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _inventoryContext.InventoryTransactions.Add(transIn);

            await _context.SaveChangesAsync();
            await _inventoryContext.SaveChangesAsync();
            
            scope.Complete();

            return Ok(new { success = true, transactionId = movement.Id, message = $"Cất hàng vào vị trí {targetLoc.Code} thành công." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(409, new { errorCode = "CONCURRENCY_CONFLICT", message = "Vị trí kệ đã thay đổi số dư bởi phiên làm việc khác, vui lòng làm mới trang." });
        }
        catch (Exception)
        {
            throw;
        }
    }

    [HttpPost("reject")]
    public async Task<IActionResult> RejectPutaway([FromBody] RejectPutawayRequestDto dto)
    {
        if (!await HasPermissionAsync("putaway_slotting.create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var proposal = await _context.PutawayProposals
            .FirstOrDefaultAsync(p => p.Id == dto.ProposalId && p.TenantId == tenantId);
        if (proposal == null)
        {
            return NotFound("Không tìm thấy đề xuất cất hàng");
        }

        proposal.Status = "REJECTED";
        proposal.Reason = $"Từ chối: {dto.ReasonCode}. Ghi chú: {dto.Note}";
        proposal.UpdatedAt = DateTime.UtcNow;
        proposal.UpdatedBy = username;

        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Đã ghi nhận từ chối đề xuất cất hàng." });
    }
}
