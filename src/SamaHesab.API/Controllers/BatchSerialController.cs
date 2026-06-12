using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Inventory.Commands;

namespace SamaHesab.API.Controllers;

/// <summary>بچ و سریال و کنترل انقضا (عمق انبار — هستهٔ ERP).</summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
public class BatchSerialController : ControllerBase
{
    private readonly IMediator _mediator;
    public BatchSerialController(IMediator mediator) => _mediator = mediator;

    [HttpGet("batches")]
    public async Task<IActionResult> Batches([FromQuery] int? productId)
        => Ok(await _mediator.Send(new GetBatchesQuery(productId)));

    [HttpPost("batches")]
    public async Task<IActionResult> SaveBatch([FromBody] SaveBatchCommand cmd)
    {
        var r = await _mediator.Send(cmd);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpGet("batches/expiring")]
    public async Task<IActionResult> Expiring([FromQuery] string today, [FromQuery] int horizonDays = 60)
        => Ok(await _mediator.Send(new GetExpiringBatchesQuery(today, horizonDays)));

    [HttpGet("serials")]
    public async Task<IActionResult> Serials([FromQuery] int? productId)
        => Ok(await _mediator.Send(new GetSerialsQuery(productId)));

    [HttpPost("serials")]
    public async Task<IActionResult> SaveSerial([FromBody] SaveSerialCommand cmd)
    {
        var r = await _mediator.Send(cmd);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }
}
