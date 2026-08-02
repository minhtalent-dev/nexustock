using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Files.Controllers;

[Authorize]
[ApiController]
[Route("api/files/storage-migrate")]
public class FileStorageMigrateController : ControllerBase
{
    private readonly IStorageMigrateService _migrate;
    private readonly IUserPermissionService _permissions;

    public FileStorageMigrateController(IStorageMigrateService migrate, IUserPermissionService permissions)
    {
        _migrate = migrate;
        _permissions = permissions;
    }

    private (bool ok, Guid userId) TryUser()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var id)) return (false, Guid.Empty);
        return (true, id);
    }

    private async Task<bool> HasManageAsync()
    {
        var (ok, id) = TryUser();
        return ok && await _permissions.HasPermissionAsync(id, "files.storage.manage");
    }

    private async Task<bool> HasPurgeAsync()
    {
        var (ok, id) = TryUser();
        return ok && await _permissions.HasPermissionAsync(id, "files.storage.migrate.purge");
    }

    [HttpPost("dry-run")]
    public async Task<IActionResult> DryRun([FromBody] MigrateDryRunRequest request)
    {
        if (!await HasManageAsync()) return Forbid();
        try
        {
            return Ok(await _migrate.DryRunAsync(request, HttpContext.RequestAborted));
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpPost("jobs")]
    public async Task<IActionResult> Start([FromBody] StartMigrateJobRequest request)
    {
        if (!await HasManageAsync()) return Forbid();
        try
        {
            var job = await _migrate.StartAsync(request, User.Identity?.Name, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id = job.JobId }, job);
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpGet("jobs/active")]
    public async Task<IActionResult> GetActive()
    {
        if (!await HasManageAsync()) return Forbid();
        var job = await _migrate.GetActiveAsync(HttpContext.RequestAborted);
        return job == null ? NoContent() : Ok(job);
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!await HasManageAsync()) return Forbid();
        var job = await _migrate.GetAsync(id, HttpContext.RequestAborted);
        return job == null ? NotFound(new { error = "MIGRATE_JOB_NOT_FOUND" }) : Ok(job);
    }

    [HttpPost("jobs/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!await HasManageAsync()) return Forbid();
        try
        {
            return Ok(await _migrate.CancelAsync(id, HttpContext.RequestAborted));
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpPost("jobs/{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id)
    {
        if (!await HasManageAsync()) return Forbid();
        try
        {
            return Ok(await _migrate.ResumeAsync(id, HttpContext.RequestAborted));
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpPost("jobs/{id:guid}/purge-source")]
    public async Task<IActionResult> Purge(Guid id)
    {
        if (!await HasPurgeAsync())
            return StatusCode(403, new { error = "MIGRATE_PURGE_FORBIDDEN", message = "Missing purge permission" });
        try
        {
            return Ok(await _migrate.PurgeSourceAsync(id, HttpContext.RequestAborted));
        }
        catch (FileDomainException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpGet("jobs/{id:guid}/errors")]
    public async Task<IActionResult> Errors(Guid id, [FromQuery] int take = 50)
    {
        if (!await HasManageAsync()) return Forbid();
        return Ok(await _migrate.GetErrorsAsync(id, take, HttpContext.RequestAborted));
    }
}
