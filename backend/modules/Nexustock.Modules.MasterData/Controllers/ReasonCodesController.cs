using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Controllers;

[ApiController]
[Route("api/master-data/reason-codes")]
public class ReasonCodesController : ControllerBase
{
    private readonly ILookupMasterDataService _service;

    public ReasonCodesController(ILookupMasterDataService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ReasonCodeDto>>> GetReasonCodes([FromQuery] string? search, [FromQuery] string? reasonType, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetReasonCodesAsync(search, reasonType, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReasonCodeDto>> GetReasonCode(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetReasonCodeAsync(id, cancellationToken);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ReasonCodeDto>> CreateReasonCode([FromBody] UpsertReasonCodeRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.CreateReasonCodeAsync(request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return CreatedAtAction(nameof(GetReasonCode), new { id = item!.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReasonCodeDto>> UpdateReasonCode(Guid id, [FromBody] UpsertReasonCodeRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.UpdateReasonCodeAsync(id, request, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            if (result.ErrorCode == "CONFLICT") return Conflict(result);
            return BadRequest(result);
        }
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteReasonCode(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteReasonCodeAsync(id, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            return BadRequest(result);
        }
        return NoContent();
    }
}
