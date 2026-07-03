using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Controllers;

[ApiController]
[Route("api/master-data/storage-locations")]
public class StorageLocationsController : ControllerBase
{
    private readonly ILookupMasterDataService _service;

    public StorageLocationsController(ILookupMasterDataService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<StorageLocationDto>>> GetLocations(
        [FromQuery] string? search,
        [FromQuery] Guid? zoneId,
        [FromQuery] bool? isLocked,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetLocationsAsync(search, zoneId, isLocked, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StorageLocationDto>> GetLocation(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetLocationAsync(id, cancellationToken);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<StorageLocationDto>> CreateLocation([FromBody] UpsertStorageLocationRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.CreateLocationAsync(request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return CreatedAtAction(nameof(GetLocation), new { id = item!.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StorageLocationDto>> UpdateLocation(Guid id, [FromBody] UpsertStorageLocationRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.UpdateLocationAsync(id, request, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            if (result.ErrorCode == "CONFLICT") return Conflict(result);
            return BadRequest(result);
        }
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLocation(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteLocationAsync(id, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            return BadRequest(result);
        }
        return NoContent();
    }
}
