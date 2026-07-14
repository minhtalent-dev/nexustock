using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Replenishment.Contexts;
using Nexustock.Modules.Replenishment.Dtos;
using Nexustock.Modules.Replenishment.Entities;
using Nexustock.Modules.Replenishment.Services;

namespace Nexustock.Modules.Replenishment.Controllers;

[Authorize]
[ApiController]
[Route("api/replenishment")]
public class ReplenishmentController : ControllerBase
{
    private readonly ReplenishmentDbContext _context;
    private readonly IReplenishmentService _replenishmentService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;

    public ReplenishmentController(
        ReplenishmentDbContext context,
        IReplenishmentService replenishmentService,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService)
    {
        _context = context;
        _replenishmentService = replenishmentService;
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

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateReplenishmentRuleDto dto)
    {
        if (!await HasPermissionAsync("replenishment.create"))
        {
            return Forbid();
        }

        if (dto.MinQty < 0 || dto.MaxQty <= dto.MinQty)
        {
            return BadRequest("Ngưỡng Min/Max không hợp lệ. MaxQty phải lớn hơn MinQty.");
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        // Check unique rule for tenant+item+location
        var exists = await _context.ReplenishmentRules
            .AnyAsync(r => r.TenantId == tenantId && r.ItemId == dto.ItemId && r.LocationId == dto.LocationId);

        if (exists)
        {
            return BadRequest("Đã tồn tại cấu hình bổ sung cho mặt hàng và vị trí kệ này");
        }

        var rule = new ReplenishmentRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = dto.ItemId,
            LocationId = dto.LocationId,
            MinQty = dto.MinQty,
            MaxQty = dto.MaxQty,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };

        _context.ReplenishmentRules.Add(rule);
        await _context.SaveChangesAsync();

        var response = new ReplenishmentRuleResponseDto
        {
            Id = rule.Id,
            TenantId = rule.TenantId,
            ItemId = rule.ItemId,
            LocationId = rule.LocationId,
            MinQty = rule.MinQty,
            MaxQty = rule.MaxQty,
            CreatedAt = rule.CreatedAt,
            CreatedBy = rule.CreatedBy
        };

        return Ok(response);
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        if (!await HasPermissionAsync("replenishment.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var rules = await _context.ReplenishmentRules
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReplenishmentRuleResponseDto
            {
                Id = r.Id,
                TenantId = r.TenantId,
                ItemId = r.ItemId,
                LocationId = r.LocationId,
                MinQty = r.MinQty,
                MaxQty = r.MaxQty,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy
            })
            .ToListAsync();

        return Ok(rules);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateTasks([FromQuery] string strategy = "FEFO")
    {
        if (!await HasPermissionAsync("replenishment.execute"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        try
        {
            var tasks = await _replenishmentService.GenerateTasksAsync(tenantId, strategy);
            var response = tasks.Select(t => new ReplenishmentTaskResponseDto
            {
                Id = t.Id,
                TenantId = t.TenantId,
                ItemId = t.ItemId,
                SourceLocationId = t.SourceLocationId,
                TargetLocationId = t.TargetLocationId,
                LotNo = t.LotNo,
                RequestedQty = t.RequestedQty,
                ActualQty = t.ActualQty,
                Status = t.Status,
                MobileTaskId = t.MobileTaskId,
                CreatedAt = t.CreatedAt,
                CreatedBy = t.CreatedBy
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks()
    {
        if (!await HasPermissionAsync("replenishment.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var tasks = await _context.ReplenishmentTasks
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new ReplenishmentTaskResponseDto
            {
                Id = t.Id,
                TenantId = t.TenantId,
                ItemId = t.ItemId,
                SourceLocationId = t.SourceLocationId,
                TargetLocationId = t.TargetLocationId,
                LotNo = t.LotNo,
                RequestedQty = t.RequestedQty,
                ActualQty = t.ActualQty,
                Status = t.Status,
                MobileTaskId = t.MobileTaskId,
                CreatedAt = t.CreatedAt,
                CreatedBy = t.CreatedBy
            })
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpPost("tasks/{id}/complete")]
    public async Task<IActionResult> CompleteTask(Guid id, [FromBody] CompleteReplenishmentTaskDto dto)
    {
        if (!await HasPermissionAsync("replenishment.execute"))
        {
            return Forbid();
        }

        if (dto.ActualQty < 0)
        {
            return BadRequest("Số lượng thực tế hoàn thành phải lớn hơn hoặc bằng 0");
        }

        var operatorName = string.IsNullOrEmpty(dto.OperatorName) 
            ? (User.Identity?.Name ?? "System") 
            : dto.OperatorName;

        try
        {
            var task = await _replenishmentService.CompleteTaskAsync(id, dto.ActualQty, operatorName);
            var response = new ReplenishmentTaskResponseDto
            {
                Id = task.Id,
                TenantId = task.TenantId,
                ItemId = task.ItemId,
                SourceLocationId = task.SourceLocationId,
                TargetLocationId = task.TargetLocationId,
                LotNo = task.LotNo,
                RequestedQty = task.RequestedQty,
                ActualQty = task.ActualQty,
                Status = task.Status,
                MobileTaskId = task.MobileTaskId,
                CreatedAt = task.CreatedAt,
                CreatedBy = task.CreatedBy
            };
            return Ok(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(409, new { errorCode = "CONCURRENCY_CONFLICT", message = "Dữ liệu vị trí hoặc nhiệm vụ đã thay đổi bởi phiên làm việc khác." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("tasks/{id}/cancel")]
    public async Task<IActionResult> CancelTask(Guid id)
    {
        if (!await HasPermissionAsync("replenishment.execute"))
        {
            return Forbid();
        }

        var username = User.Identity?.Name ?? "System";

        try
        {
            var task = await _replenishmentService.CancelTaskAsync(id, username);
            var response = new ReplenishmentTaskResponseDto
            {
                Id = task.Id,
                TenantId = task.TenantId,
                ItemId = task.ItemId,
                SourceLocationId = task.SourceLocationId,
                TargetLocationId = task.TargetLocationId,
                LotNo = task.LotNo,
                RequestedQty = task.RequestedQty,
                ActualQty = task.ActualQty,
                Status = task.Status,
                MobileTaskId = task.MobileTaskId,
                CreatedAt = task.CreatedAt,
                CreatedBy = task.CreatedBy
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
