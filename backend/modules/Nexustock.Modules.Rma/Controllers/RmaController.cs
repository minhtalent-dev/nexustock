using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Nexustock.Modules.Rma.Services;
using Nexustock.Modules.Rma.DTOs;
using System;
using System.Threading.Tasks;

namespace Nexustock.Modules.Rma.Controllers;

[ApiController]
[Route("api/rma")]
[Authorize]
public class RmaController : ControllerBase
{
    private readonly IRmaService _rmaService;

    public RmaController(IRmaService rmaService)
    {
        _rmaService = rmaService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRmaDto dto)
    {
        var result = await _rmaService.CreateRmaAsync(dto, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, [FromBody] ReceiveRmaDto dto)
    {
        var result = await _rmaService.ReceiveRmaAsync(id, dto, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpPost("{id:guid}/qc")]
    public async Task<IActionResult> ProcessQc(Guid id, [FromBody] ProcessRmaQcDto dto)
    {
        var result = await _rmaService.ProcessRmaQcAsync(id, dto, User.Identity!.Name!);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _rmaService.GetRmaDetailsAsync(id);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _rmaService.GetAllRmasAsync();
        return Ok(result);
    }
}
