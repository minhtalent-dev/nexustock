using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.ErpIntegration.Contexts;
using Nexustock.Modules.ErpIntegration.Entities;
using Nexustock.Modules.ErpIntegration.Services;

namespace Nexustock.Modules.ErpIntegration.Controllers;

[Authorize]
[ApiController]
[Route("api/integration")]
public class IntegrationAdminController : ControllerBase
{
    private readonly ErpIntegrationDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IImportPreviewService _previewService;
    private readonly IImportCommitService _commitService;

    public IntegrationAdminController(
        ErpIntegrationDbContext context,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IImportPreviewService previewService,
        IImportCommitService commitService)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _previewService = previewService;
        _commitService = commitService;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    // GET /api/integration/messages
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string? status,
        [FromQuery] string? messageType,
        [FromQuery] string? externalSystem,
        [FromQuery] string? traceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!await HasPermissionAsync("integration.view"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var query = _context.IntegrationMessages
            .Where(m => m.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(m => m.Status == status);
        }
        if (!string.IsNullOrEmpty(messageType))
        {
            query = query.Where(m => m.MessageType == messageType);
        }
        if (!string.IsNullOrEmpty(externalSystem))
        {
            query = query.Where(m => m.ExternalSystem == externalSystem);
        }
        if (!string.IsNullOrEmpty(traceId))
        {
            query = query.Where(m => m.TraceId == traceId);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, items, page, pageSize });
    }

    // GET /api/integration/mappings
    [HttpGet("mappings")]
    public async Task<IActionResult> GetMappings(
        [FromQuery] string? mappingType,
        [FromQuery] string? externalSystem,
        [FromQuery] string? externalCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!await HasPermissionAsync("integration.view"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var query = _context.IntegrationMappings
            .Where(m => m.TenantId == tenantId);

        if (!string.IsNullOrEmpty(mappingType))
        {
            query = query.Where(m => m.MappingType == mappingType);
        }
        if (!string.IsNullOrEmpty(externalSystem))
        {
            query = query.Where(m => m.ExternalSystem == externalSystem);
        }
        if (!string.IsNullOrEmpty(externalCode))
        {
            query = query.Where(m => m.ExternalCode.Contains(externalCode));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.MappingType)
            .ThenBy(m => m.ExternalCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, items, page, pageSize });
    }

    // POST /api/integration/mappings
    [HttpPost("mappings")]
    public async Task<IActionResult> CreateMapping([FromBody] CreateMappingDto dto)
    {
        if (!await HasPermissionAsync("integration.import"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();

        var exists = await _context.IntegrationMappings
            .AnyAsync(m => m.TenantId == tenantId && 
                           m.ExternalSystem == dto.ExternalSystem && 
                           m.MappingType == dto.MappingType && 
                           m.ExternalCode == dto.ExternalCode);

        if (exists)
        {
            return BadRequest(new { errorCode = "mapping.alreadyExists", message = "Mapping alias already exists." });
        }

        var mapping = new IntegrationMapping
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalSystem = dto.ExternalSystem,
            MappingType = dto.MappingType,
            ExternalCode = dto.ExternalCode,
            InternalCode = dto.InternalCode,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.IntegrationMappings.Add(mapping);
        await _context.SaveChangesAsync();

        return Created("", mapping);
    }

    // PUT /api/integration/mappings/{id}
    [HttpPut("mappings/{id:guid}")]
    public async Task<IActionResult> UpdateMapping(Guid id, [FromBody] UpdateMappingDto dto)
    {
        if (!await HasPermissionAsync("integration.import"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var mapping = await _context.IntegrationMappings
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId);

        if (mapping == null) return NotFound();

        mapping.InternalCode = dto.InternalCode;
        mapping.Status = dto.Status;
        mapping.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(mapping);
    }

    // DELETE /api/integration/mappings/{id}
    [HttpDelete("mappings/{id:guid}")]
    public async Task<IActionResult> DeleteMapping(Guid id)
    {
        if (!await HasPermissionAsync("integration.import"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var mapping = await _context.IntegrationMappings
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId);

        if (mapping == null) return NotFound();

        _context.IntegrationMappings.Remove(mapping);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/integration/import/preview
    [HttpPost("import/preview")]
    public async Task<IActionResult> PreviewImport([FromQuery] string externalSystem, IFormFile file)
    {
        if (!await HasPermissionAsync("integration.import"))
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "File rỗng hoặc không tìm thấy." });
        }

        var tenantId = GetTenantId();
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var csvContent = await reader.ReadToEndAsync();

        var result = await _previewService.PreviewMappingsAsync(tenantId, externalSystem, csvContent);
        return Ok(result);
    }

    // POST /api/integration/import/commit/{jobId}
    [HttpPost("import/commit/{jobId:guid}")]
    public async Task<IActionResult> CommitImport(Guid jobId)
    {
        if (!await HasPermissionAsync("integration.import"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var result = await _commitService.CommitImportAsync(tenantId, jobId);

        if (result.Status == "failed")
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(result);
    }
}

public class CreateMappingDto
{
    public string ExternalSystem { get; set; } = null!;
    public string MappingType { get; set; } = null!;
    public string ExternalCode { get; set; } = null!;
    public string InternalCode { get; set; } = null!;
}

public class UpdateMappingDto
{
    public string InternalCode { get; set; } = null!;
    public string Status { get; set; } = null!;
}
