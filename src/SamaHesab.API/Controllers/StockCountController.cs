using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Application.Inventory.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// انبارگردانی سندی (کار #۲۸): شروع شمارش (snapshot موجودی)، ثبت تعداد شمرده‌شده،
/// و نهایی‌سازی (تبدیل مغایرت‌ها به تعدیل موجودی + سند حسابداری خودکار).
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class StockCountController : ControllerBase
{
    private readonly IMediator _mediator;
    public StockCountController(IMediator mediator) => _mediator = mediator;

    public record StartRequest(int WarehouseId, string Date);

    /// <summary>شروع انبارگردانی یک انبار (موجودی سیستمی snapshot می‌شود).</summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new StartStockCountCommand(req.WarehouseId, req.Date), ct);
        return r.Succeeded ? Ok(new { sessionId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>جزئیات سند انبارگردانی + مغایرت‌ها.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetStockCountQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    public record CountRequest(int ProductId, decimal CountedQty);

    /// <summary>ثبت تعداد شمرده‌شده‌ی یک کالا.</summary>
    [HttpPost("{id:int}/count")]
    public async Task<IActionResult> Count(int id, [FromBody] CountRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new SetStockCountLineCommand(id, req.ProductId, req.CountedQty), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>نهایی‌سازی: مغایرت‌ها به تعدیل موجودی تبدیل می‌شوند.</summary>
    [HttpPost("{id:int}/post")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new PostStockCountCommand(id), ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }
}
