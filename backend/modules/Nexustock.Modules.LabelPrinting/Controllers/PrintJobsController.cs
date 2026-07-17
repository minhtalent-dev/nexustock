using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.LabelPrinting.DTOs;
using Nexustock.Modules.LabelPrinting.Services;

namespace Nexustock.Modules.LabelPrinting.Controllers;

[Authorize]
[ApiController]
[Route("api/printing/jobs")]
public class PrintJobsController : ControllerBase
{
    private readonly ILabelPrintingService _service;
    private readonly IUserPermissionService _permissionService;

    public PrintJobsController(ILabelPrintingService service, IUserPermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] CreatePrintJobRequest request, CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync("label_printing.print")) return Forbid();

        var (result, item) = await _service.CreateJobAsync(request, GetUsername(), cancellationToken);
        if (!result.Success) return BadRequest(result);

        return Ok(item);
    }

    [HttpPost("{id:guid}/reprint")]
    public async Task<IActionResult> ReprintJob(Guid id, [FromBody] ReprintJobRequest request, CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync("label_printing.reprint")) return Forbid();

        var (result, item) = await _service.ReprintJobAsync(id, request, GetUsername(), cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "JOB_NOT_FOUND") return NotFound(result);
            if (result.ErrorCode == "REPRINT_LIMIT_EXCEEDED") return Conflict(result);
            return BadRequest(result);
        }

        return Ok(item);
    }

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    private string GetUsername() => User.Identity?.Name ?? "System";
}
