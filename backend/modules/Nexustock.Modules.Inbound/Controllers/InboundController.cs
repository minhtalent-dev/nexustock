using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Dtos;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Inbound.Services;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.Inventory.Contexts;

namespace Nexustock.Modules.Inbound.Controllers;

[Authorize]
[ApiController]
[Route("api/inbound/orders")]
public class InboundController : ControllerBase
{
    private readonly InboundDbContext _context;
    private readonly MasterDataDbContext _masterContext;
    private readonly Services.ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IInventoryService _inventoryService;
    private readonly InventoryDbContext _inventoryContext;
    private readonly IInboundLineImportService _lineImportService;

    public InboundController(
        InboundDbContext context, 
        MasterDataDbContext masterContext, 
        Services.ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IInventoryService inventoryService,
        InventoryDbContext inventoryContext,
        IInboundLineImportService lineImportService)
    {
        _context = context;
        _masterContext = masterContext;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _inventoryService = inventoryService;
        _inventoryContext = inventoryContext;
        _lineImportService = lineImportService;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;

        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] string? status)
    {
        if (!await HasPermissionAsync("Inbound.Orders.View"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var query = _context.InboundOrders
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<InboundOrderStatus>(status, true, out var orderStatus))
        {
            query = query.Where(o => o.Status == orderStatus);
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

        // Query MasterData info to map names
        var partnerIds = orders.Select(o => o.PartnerId).Distinct().ToList();
        var itemIds = orders.SelectMany(o => o.Items).Select(i => i.ItemId).Distinct().ToList();
        var uomIds = orders.SelectMany(o => o.Items).Select(i => i.UomId).Distinct().ToList();

        var partners = await _masterContext.Partners
            .Where(p => partnerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var products = await _masterContext.Products
            .Where(p => itemIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Code });

        var uoms = await _masterContext.Uoms
            .Where(u => uomIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var response = orders.Select(o => new InboundOrderResponseDto
        {
            Id = o.Id,
            OrderNo = o.OrderNo,
            PartnerId = o.PartnerId,
            PartnerName = partners.TryGetValue(o.PartnerId, out var pName) ? pName : "Unknown Partner",
            Status = o.Status.ToString(),
            CreatedAt = o.CreatedAt,
            CreatedBy = o.CreatedBy,
            Items = o.Items.Select(i => new InboundOrderItemResponseDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                ItemName = products.TryGetValue(i.ItemId, out var prod) ? prod.Name : "Unknown Item",
                ItemCode = products.TryGetValue(i.ItemId, out var prod2) ? prod2.Code : "Unknown Code",
                UomId = i.UomId,
                UomName = uoms.TryGetValue(i.UomId, out var uName) ? uName : "Unknown UOM",
                ExpectedQty = i.ExpectedQty,
                ReceivedQty = i.ReceivedQty,
                Tolerance = i.Tolerance
            }).ToList()
        }).ToList();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        if (!await HasPermissionAsync("Inbound.Orders.View"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var order = await _context.InboundOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId);

        if (order == null) return NotFound();

        var partner = await _masterContext.Partners.FindAsync(order.PartnerId);
        var itemIds = order.Items.Select(i => i.ItemId).ToList();
        var uomIds = order.Items.Select(i => i.UomId).ToList();

        var products = await _masterContext.Products
            .Where(p => itemIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Code });

        var uoms = await _masterContext.Uoms
            .Where(u => uomIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var response = new InboundOrderResponseDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            PartnerId = order.PartnerId,
            PartnerName = partner?.Name ?? "Unknown Partner",
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt,
            CreatedBy = order.CreatedBy,
            Items = order.Items.Select(i => new InboundOrderItemResponseDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                ItemName = products.TryGetValue(i.ItemId, out var prod) ? prod.Name : "Unknown Item",
                ItemCode = products.TryGetValue(i.ItemId, out var prod2) ? prod2.Code : "Unknown Code",
                UomId = i.UomId,
                UomName = uoms.TryGetValue(i.UomId, out var uName) ? uName : "Unknown UOM",
                ExpectedQty = i.ExpectedQty,
                ReceivedQty = i.ReceivedQty,
                Tolerance = i.Tolerance
            }).ToList()
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateInboundOrderDto dto)
    {
        if (!await HasPermissionAsync("Inbound.Orders.Create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var orderNo = dto.OrderNo;
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            orderNo = $"IO-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        }

        var order = new InboundOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNo = orderNo,
            PartnerId = dto.PartnerId,
            Status = InboundOrderStatus.Open,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username,
            Items = dto.Items.Select(i => new InboundOrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = i.ItemId,
                UomId = i.UomId,
                ExpectedQty = i.ExpectedQty,
                ReceivedQty = 0,
                Tolerance = i.Tolerance
            }).ToList()
        };

        _context.InboundOrders.Add(order);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, new { id = order.Id, orderNo = order.OrderNo });
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> ReceiveItem(Guid id, [FromBody] ReceiveItemDto dto)
    {
        if (!await HasPermissionAsync("Inbound.Orders.Receive"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";
        var traceId = HttpContext.TraceIdentifier;

        var order = await _context.InboundOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId);

        if (order == null) return NotFound("Inbound order not found");
        if (order.Status == InboundOrderStatus.Completed || order.Status == InboundOrderStatus.Cancelled)
        {
            return BadRequest("Cannot receive items for a completed or cancelled order");
        }

        var item = order.Items.FirstOrDefault(i => i.ItemId == dto.ItemId);
        if (item == null) return BadRequest("Item not found in this inbound order");

        // Verify Tolerance
        var limitQty = item.ExpectedQty * (1 + item.Tolerance);
        if (item.ReceivedQty + dto.ReceivedQty > limitQty)
        {
            // Requires Inbound.Orders.Approve
            if (!await HasPermissionAsync("Inbound.Orders.Approve"))
            {
                return BadRequest("Received quantity exceeds allowed tolerance limit");
            }
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Create or retrieve Lot
            var lot = await _context.Lots
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.LotNo == dto.LotNo && l.ItemId == dto.ItemId);

            if (lot == null)
            {
                lot = new Lot
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LotNo = dto.LotNo,
                    ItemId = dto.ItemId,
                    ExpiryDate = dto.ExpiryDate,
                    ProductionDate = dto.ProductionDate,
                    QcStatus = LotQcStatus.Unspec
                };
                _context.Lots.Add(lot);
            }

            // 2. Create Inventory Transaction
            var invTrans = new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = dto.ItemId,
                LotNo = dto.LotNo,
                TransactionType = "RECEIVE",
                Qty = dto.ReceivedQty,
                ToLocationId = dto.ToLocationId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username,
                TraceId = traceId
            };
            _context.InventoryTransactions.Add(invTrans);

            // 3. Update Order Item
            item.ReceivedQty += dto.ReceivedQty;

            // 4. Update Order Status
            var allCompleted = order.Items.All(i => i.ReceivedQty >= i.ExpectedQty);
            if (allCompleted)
            {
                order.Status = InboundOrderStatus.Completed;
            }
            else
            {
                order.Status = InboundOrderStatus.Receiving;
            }
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = username;

            // 5. Record Inventory Balance with shared connection and transaction
            _inventoryContext.Database.SetDbConnection(_context.Database.GetDbConnection());
            if (_context.Database.CurrentTransaction != null)
            {
                await _inventoryContext.Database.UseTransactionAsync(_context.Database.CurrentTransaction.GetDbTransaction());
            }
            await _inventoryService.RecordReceiptAsync(tenantId, dto.ItemId, dto.LotNo, dto.ToLocationId, dto.ReceivedQty, username, traceId);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Received successfully", itemReceivedQty = item.ReceivedQty, orderStatus = order.Status.ToString() });
        }
        catch (InvalidOperationException ex) when (ex.Message == "LOCATION_OVER_CAPACITY")
        {
            await transaction.RollbackAsync();
            return BadRequest(new { errorCode = "LOCATION_OVER_CAPACITY", message = "Received quantity exceeds max capacity of destination location" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("/api/inbound/{id:guid}/lines/import/preview")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> PreviewLineImport(Guid id, IFormFile file)
    {
        if (!await HasPermissionAsync("Inbound.Orders.Create"))
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Không tìm thấy file." });

        var username = User.Identity?.Name ?? "SYSTEM";
        await using var stream = file.OpenReadStream();
        var result = await _lineImportService.PreviewImportAsync(id, file.ContentType, stream, file.FileName, username, HttpContext.RequestAborted);
        if (string.Equals(result.ErrorCsvContent, "IMPORT_TOO_LARGE", StringComparison.Ordinal))
            return BadRequest(new { error = "IMPORT_ROW_LIMIT_EXCEEDED", message = "Import exceeds 5000 data rows." });
        if (string.Equals(result.ErrorCsvContent, "IMPORT_TEMPLATE_VERSION_UNSUPPORTED", StringComparison.Ordinal))
            return BadRequest(new { error = "IMPORT_TEMPLATE_VERSION_UNSUPPORTED" });
        if (!result.Success && result.BatchId == Guid.Empty)
            return Conflict(result);

        return Ok(result);
    }

    [HttpPost("/api/inbound/{id:guid}/lines/import/commit")]
    public async Task<IActionResult> CommitLineImport(Guid id, [FromBody] MasterData.DTOs.CommitImportRequest request)
    {
        if (!await HasPermissionAsync("Inbound.Orders.Create"))
            return Forbid();

        var username = User.Identity?.Name ?? "SYSTEM";
        var result = await _lineImportService.CommitImportAsync(id, request.BatchId, username, HttpContext.RequestAborted);
        if (result.Success) return Ok(result);

        return result.ErrorCsvContent switch
        {
            "IMPORT_BATCH_NOT_FOUND" => NotFound(result),
            "IMPORT_BATCH_EXPIRED" or "IMPORT_BATCH_HAS_ERRORS" or "IMPORT_BATCH_ALREADY_COMMITTED" or
                "IMPORT_TARGET_MISMATCH" or "IMPORT_TARGET_STATE_INVALID" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    [HttpGet("/api/inbound/{id:guid}/lines/import/errors/{batchId:guid}")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportLineImportErrors(Guid id, Guid batchId)
    {
        if (!await HasPermissionAsync("Inbound.Orders.Create"))
            return Forbid();

        var username = User.Identity?.Name ?? "SYSTEM";
        var csv = await _lineImportService.ExportErrorCsvAsync(id, batchId, username, HttpContext.RequestAborted);
        if (csv == null)
            return NotFound(new { error = "Không tìm thấy batch hoặc batch không có lỗi." });

        var bom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bom, "text/csv", $"errors_{batchId}.csv");
    }
}
