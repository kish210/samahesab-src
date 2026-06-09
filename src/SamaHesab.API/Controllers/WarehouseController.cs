using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Application.Inventory.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// ماژول انبار (فاز ۲): رسید ورود، حواله خروج، تعدیل/انبارگردانی — کلاینت انبار از طریق این API.
/// منطق در لایه‌ی Application (CQRS) است؛ بهای تمام‌شده به‌روش میانگین موزون.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IMediator _mediator;
    public WarehouseController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new GetWarehousesQuery(), ct));

    [HttpGet("stock")]
    public async Task<IActionResult> Stock([FromQuery] int warehouseId, [FromQuery] string? q, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWarehouseStockQuery(warehouseId, q), ct));

    [HttpPost("receive")]
    public async Task<IActionResult> Receive([FromBody] ReceiveStockCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("issue")]
    public async Task<IActionResult> Issue([FromBody] IssueStockCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust([FromBody] AdjustStockCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>انتقال یک کالا بین دو انبار.</summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferStockCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
