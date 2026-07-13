using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Exceptions.Dtos;
using Nexustock.Modules.Exceptions.Entities;
using Nexustock.Modules.Exceptions.Services;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Exceptions.Controllers;

[Authorize]
[ApiController]
[Route("api/exceptions")]
public class ExceptionsController : ControllerBase
{
    private readonly ExceptionsDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IHttpClientFactory _httpClientFactory;

    public ExceptionsController(
        ExceptionsDbContext context,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _httpClientFactory = httpClientFactory;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpPost]
    public async Task<IActionResult> CreateException([FromBody] CreateExceptionRequest dto)
    {
        if (!await HasPermissionAsync("exception_framework_mvp.create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        // Generate Code EX-YYMMDD-XXXX
        var dateStr = DateTime.UtcNow.ToString("yyMMdd");
        var countToday = await _context.OperationalExceptions
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.Code.StartsWith($"EX-{dateStr}-"))
            .CountAsync();
        var code = $"EX-{dateStr}-{(countToday + 1):D4}";

        var exception = new OperationalException
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Type = dto.Type,
            Severity = dto.Severity,
            Status = "Open",
            ReferenceType = dto.ReferenceType,
            ReferenceId = dto.ReferenceId,
            LocationId = dto.LocationId,
            LotNo = dto.LotNo,
            Qty = dto.Qty,
            ReasonCode = dto.ReasonCode,
            Note = dto.Note,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };

        _context.OperationalExceptions.Add(exception);

        var @event = new ExceptionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExceptionId = exception.Id,
            Transition = "CREATE",
            Actor = username,
            Note = "Ngoai le khoi tao",
            CreatedAt = DateTime.UtcNow
        };
        _context.ExceptionEvents.Add(@event);

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetExceptionById), new { id = exception.Id }, new ExceptionResponse
        {
            Id = exception.Id,
            Code = exception.Code,
            Type = exception.Type,
            Severity = exception.Severity,
            Status = exception.Status,
            ReferenceType = exception.ReferenceType,
            ReferenceId = exception.ReferenceId,
            LocationId = exception.LocationId,
            LotNo = exception.LotNo,
            Qty = exception.Qty,
            ReasonCode = exception.ReasonCode,
            Note = exception.Note,
            CreatedAt = exception.CreatedAt,
            CreatedBy = exception.CreatedBy
        });
    }

    [HttpGet("open")]
    public async Task<IActionResult> GetOpenExceptions(
        [FromQuery] string? severity,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!await HasPermissionAsync("exception_framework_mvp.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var query = _context.OperationalExceptions
            .Where(e => e.TenantId == tenantId && (e.Status == "Open" || e.Status == "In_Progress"));

        if (!string.IsNullOrWhiteSpace(severity)) query = query.Where(e => e.Severity == severity);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(e => e.Type == type);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExceptionResponse
            {
                Id = e.Id,
                Code = e.Code,
                Type = e.Type,
                Severity = e.Severity,
                Status = e.Status,
                ReferenceType = e.ReferenceType,
                ReferenceId = e.ReferenceId,
                LocationId = e.LocationId,
                LotNo = e.LotNo,
                Qty = e.Qty,
                ReasonCode = e.ReasonCode,
                Note = e.Note,
                CreatedAt = e.CreatedAt,
                CreatedBy = e.CreatedBy,
                UpdatedAt = e.UpdatedAt,
                UpdatedBy = e.UpdatedBy
            })
            .ToListAsync();

        return Ok(new { items, totalCount });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExceptionById(Guid id)
    {
        if (!await HasPermissionAsync("exception_framework_mvp.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var exception = await _context.OperationalExceptions
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id);

        if (exception == null) return NotFound("Khong tim thay ngoai le");

        return Ok(new ExceptionResponse
        {
            Id = exception.Id,
            Code = exception.Code,
            Type = exception.Type,
            Severity = exception.Severity,
            Status = exception.Status,
            ReferenceType = exception.ReferenceType,
            ReferenceId = exception.ReferenceId,
            LocationId = exception.LocationId,
            LotNo = exception.LotNo,
            Qty = exception.Qty,
            ReasonCode = exception.ReasonCode,
            Note = exception.Note,
            CreatedAt = exception.CreatedAt,
            CreatedBy = exception.CreatedBy,
            UpdatedAt = exception.UpdatedAt,
            UpdatedBy = exception.UpdatedBy
        });
    }

    [HttpGet("{id:guid}/events")]
    public async Task<IActionResult> GetExceptionEvents(Guid id)
    {
        if (!await HasPermissionAsync("exception_framework_mvp.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var events = await _context.ExceptionEvents
            .Where(e => e.TenantId == tenantId && e.ExceptionId == id)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new ExceptionEventResponse
            {
                Id = e.Id,
                Transition = e.Transition,
                Actor = e.Actor,
                Note = e.Note,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        return Ok(events);
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> AssignException(Guid id, [FromBody] AssignExceptionRequest dto)
    {
        if (!await HasPermissionAsync("exception_framework_mvp.update"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var exception = await _context.OperationalExceptions
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id);
        if (exception == null) return NotFound("Khong tim thay ngoai le");

        if (exception.Status == "Resolved" || exception.Status == "Cancelled")
        {
            return BadRequest("Ngoai le da ket thuc, khong the gan");
        }

        exception.Status = "In_Progress";
        exception.UpdatedAt = DateTime.UtcNow;
        exception.UpdatedBy = username;

        var assignment = new ExceptionAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExceptionId = exception.Id,
            Owner = dto.Owner,
            SlaDeadline = DateTime.UtcNow.AddHours(dto.SlaHours),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };
        _context.ExceptionAssignments.Add(assignment);

        var @event = new ExceptionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExceptionId = exception.Id,
            Transition = "ASSIGN",
            Actor = username,
            Note = $"Gan cho {dto.Owner} voi SLA {dto.SlaHours} gio",
            CreatedAt = DateTime.UtcNow
        };
        _context.ExceptionEvents.Add(@event);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Gan nguoi xu ly thanh cong" });
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> ResolveException(Guid id, [FromBody] ResolveExceptionRequest dto)
    {
        if (!await HasPermissionAsync("exception_framework_mvp.approve"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var exception = await _context.OperationalExceptions
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id);
        if (exception == null) return NotFound("Khong tim thay ngoai le");

        if (exception.Status == "Resolved" || exception.Status == "Cancelled")
        {
            return BadRequest("Ngoai le da duoc dong");
        }

        // Execute Corrective Transaction (Giao tiep Module)
        if (dto.Action == "CORRECTIVE_TRANSACTION")
        {
            // Verify Qty, LocationId, ItemId, LotNo phai hop le
            if (!exception.LocationId.HasValue || string.IsNullOrWhiteSpace(exception.LotNo))
            {
                return BadRequest("Khong du thong tin (Location/Lot) de thuc hien corrective transaction");
            }

            var client = _httpClientFactory.CreateClient();
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Add("Authorization", authHeader);
            }

            // Gọi API adjust của Inventory
            var response = await client.PostAsJsonAsync("http://localhost:5024/api/inventory/adjust", new
            {
                itemId = exception.ReferenceId, // referenceId cua Exception luu ItemId
                lotNo = exception.LotNo,
                locationId = exception.LocationId.Value,
                qty = exception.Qty, // quantity can adjust (+/-)
                reasonCode = dto.ReasonCode,
                idempotencyKey = exception.Id.ToString() // Dung Exception ID lam IdempotencyKey
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                return BadRequest(new { message = "Loi khi goi dieu chinh ton kho cua Inventory module", detail = errorMsg });
            }
        }

        exception.Status = "Resolved";
        exception.UpdatedAt = DateTime.UtcNow;
        exception.UpdatedBy = username;

        // Close assignments
        var assignments = await _context.ExceptionAssignments
            .Where(a => a.TenantId == tenantId && a.ExceptionId == exception.Id && a.Status == "Pending")
            .ToListAsync();
        foreach (var assignment in assignments)
        {
            assignment.Status = "Completed";
        }

        var @event = new ExceptionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExceptionId = exception.Id,
            Transition = "RESOLVE",
            Actor = username,
            Note = $"Giai quyet ngoai le voi action: {dto.Action}, reason: {dto.ReasonCode}",
            CreatedAt = DateTime.UtcNow
        };
        _context.ExceptionEvents.Add(@event);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Giai quyet va dong ngoai le thanh cong" });
    }
}
