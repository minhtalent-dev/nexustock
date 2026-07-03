using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Controllers;

[ApiController]
[Route("api/master-data/storage-zones")]
public class StorageZonesController : ControllerBase
{
    private readonly ILookupMasterDataService _service;

    public StorageZonesController(ILookupMasterDataService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<StorageZoneDto>>> GetZones([FromQuery] string? search, [FromQuery] Guid? warehouseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetZonesAsync(search, warehouseId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StorageZoneDto>> GetZone(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetZoneAsync(id, cancellationToken);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<StorageZoneDto>> CreateZone([FromBody] UpsertStorageZoneRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.CreateZoneAsync(request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return CreatedAtAction(nameof(GetZone), new { id = item!.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StorageZoneDto>> UpdateZone(Guid id, [FromBody] UpsertStorageZoneRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.UpdateZoneAsync(id, request, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            if (result.ErrorCode == "CONFLICT") return Conflict(result);
            return BadRequest(result);
        }
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteZone(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteZoneAsync(id, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            return BadRequest(result);
        }
        return NoContent();
    }
}
