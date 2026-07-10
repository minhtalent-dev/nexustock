using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Dtos;
using Nexustock.Modules.Inbound.Services;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.Modules.Inbound.Controllers;

[Authorize]
[ApiController]
[Route("api/lots")]
public class LotsController : ControllerBase
{
    private readonly InboundDbContext _context;
    private readonly MasterDataDbContext _masterContext;
    private readonly ITenantProvider _tenantProvider;

    public LotsController(
        InboundDbContext context, 
        MasterDataDbContext masterContext, 
        ITenantProvider tenantProvider)
    {
        _context = context;
        _masterContext = masterContext;
        _tenantProvider = tenantProvider;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;

        const string sql = @"
            SELECT DISTINCT p.""Name""
            FROM ""RolePermissions"" rp
            INNER JOIN ""Permissions"" p ON rp.""PermissionId"" = p.""Id""
            INNER JOIN ""UserRoles"" ur ON rp.""RoleId"" = ur.""RoleId""
            WHERE ur.""UserId"" = {0}";

        var permissions = await _context.Database
            .SqlQueryRaw<string>(sql, userId)
            .ToListAsync();

        return permissions.Contains(permissionName);
    }

    [HttpGet("{lotNo}")]
    public async Task<IActionResult> GetByLotNo(string lotNo)
    {
        if (!await HasPermissionAsync("Inbound.Lots.View"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var lots = await _context.Lots
            .Where(l => l.LotNo == lotNo && l.TenantId == tenantId)
            .ToListAsync();

        if (lots.Count == 0) return NotFound("Lot not found");

        var itemIds = lots.Select(l => l.ItemId).ToList();
        var products = await _masterContext.Products
            .Where(p => itemIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Code });

        var response = lots.Select(l => new LotResponseDto
        {
            Id = l.Id,
            LotNo = l.LotNo,
            ItemId = l.ItemId,
            ItemName = products.TryGetValue(l.ItemId, out var prod) ? prod.Name : "Unknown Item",
            ItemCode = products.TryGetValue(l.ItemId, out var prod2) ? prod2.Code : "Unknown Code",
            ExpiryDate = l.ExpiryDate,
            ProductionDate = l.ProductionDate,
            QcStatus = l.QcStatus.ToString()
        }).ToList();

        return Ok(response);
    }
}
