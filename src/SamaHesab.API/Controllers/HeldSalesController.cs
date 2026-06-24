using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Modules.POS.Application;

namespace SamaHesab.API.Controllers;

/// <summary>کار #۳۳ — فاکتورهای معلق POS: تعلیق سبد، فهرست، فراخوان، حذف.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class HeldSalesController : ControllerBase
{
    private readonly IMediator _mediator;
    public HeldSalesController(IMediator mediator) => _mediator = mediator;

    public record HoldRequest(string Label, string Payload, decimal Total);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new GetHeldSalesQuery(), ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetHeldSaleQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Hold([FromBody] HoldRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new HoldSaleCommand(req.Label, req.Payload, req.Total), ct);
        return r.Succeeded ? Ok(new { heldSaleId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteHeldSaleCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
