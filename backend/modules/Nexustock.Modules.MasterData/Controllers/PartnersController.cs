using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Controllers;

[ApiController]
[Route("api/master-data/partners")]
public class PartnersController : ControllerBase
{
    private readonly ILookupMasterDataService _service;

    public PartnersController(ILookupMasterDataService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PartnerDto>>> GetPartners([FromQuery] string? search, [FromQuery] string? partnerType, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetPartnersAsync(search, partnerType, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartnerDto>> GetPartner(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetPartnerAsync(id, cancellationToken);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<PartnerDto>> CreatePartner([FromBody] UpsertPartnerRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.CreatePartnerAsync(request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return CreatedAtAction(nameof(GetPartner), new { id = item!.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PartnerDto>> UpdatePartner(Guid id, [FromBody] UpsertPartnerRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.UpdatePartnerAsync(id, request, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            if (result.ErrorCode == "CONFLICT") return Conflict(result);
            return BadRequest(result);
        }
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePartner(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeletePartnerAsync(id, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            return BadRequest(result);
        }
        return NoContent();
    }
}
