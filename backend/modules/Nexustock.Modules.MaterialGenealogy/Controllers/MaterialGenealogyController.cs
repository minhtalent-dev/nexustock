using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Nexustock.Modules.MaterialGenealogy.Services;
using Nexustock.Modules.MaterialGenealogy.DTOs;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MaterialGenealogy.Controllers;

[ApiController]
[Route("api/genealogy")]
[Authorize]
public class MaterialGenealogyController : ControllerBase
{
    private readonly IMaterialGenealogyService _genealogyService;
    private readonly ITenantProvider _tenantProvider;

    public MaterialGenealogyController(IMaterialGenealogyService genealogyService, ITenantProvider tenantProvider)
    {
        _genealogyService = genealogyService;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("relations")]
    public async Task<IActionResult> CreateRelation([FromBody] CreateLotRelationDto dto)
    {
        var tenantId = _tenantProvider.TenantId;
        var username = User.Identity!.Name!;
        await _genealogyService.CreateRelationAsync(tenantId, username, dto);
        return Ok(new { message = "Tạo liên kết phả hệ Lot thành công." });
    }

    [HttpGet("lots/{lotNo}/tree")]
    public async Task<IActionResult> GetTree(string lotNo)
    {
        var tenantId = _tenantProvider.TenantId;
        var tree = await _genealogyService.GetLotTreeAsync(tenantId, lotNo);
        return Ok(tree);
    }

    [HttpPost("hold-branch")]
    public async Task<IActionResult> HoldBranch([FromBody] HoldBranchDto dto)
    {
        var tenantId = _tenantProvider.TenantId;
        var username = User.Identity!.Name!;
        await _genealogyService.HoldBranchAsync(tenantId, username, dto);
        return Ok(new { message = "Phong tỏa nhánh Lot thành công." });
    }
}
