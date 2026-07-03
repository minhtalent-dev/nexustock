using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.MasterData.DTOs;
using Nexustock.Modules.MasterData.Services;

namespace Nexustock.Modules.MasterData.Controllers;

[ApiController]
[Route("api/master-data/warehouses")]
public class WarehousesController : ControllerBase
{
    private readonly ILookupMasterDataService _service;

    public WarehousesController(ILookupMasterDataService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<WarehouseDto>>> GetWarehouses([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetWarehousesAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WarehouseDto>> GetWarehouse(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetWarehouseAsync(id, cancellationToken);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<WarehouseDto>> CreateWarehouse([FromBody] UpsertWarehouseRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.CreateWarehouseAsync(request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return CreatedAtAction(nameof(GetWarehouse), new { id = item!.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WarehouseDto>> UpdateWarehouse(Guid id, [FromBody] UpsertWarehouseRequest request, CancellationToken cancellationToken)
    {
        var (result, item) = await _service.UpdateWarehouseAsync(id, request, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            if (result.ErrorCode == "CONFLICT") return Conflict(result);
            return BadRequest(result);
        }
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWarehouse(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteWarehouseAsync(id, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(result);
            return BadRequest(result);
        }
        return NoContent();
    }
}
