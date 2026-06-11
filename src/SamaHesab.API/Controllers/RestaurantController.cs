using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Restaurant.Commands;
using SamaHesab.Application.Restaurant.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// ماژول رستوران (v2): سالن/میز، سفارش باز روی میز، ارسال به آشپزخانه (KDS) و تسویه.
/// همه‌ی منطق در لایه‌ی Application (CQRS) است تا کلاینت دسکتاپ و وب یکسان از آن استفاده کنند.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RestaurantController : ControllerBase
{
    private readonly IMediator _mediator;
    public RestaurantController(IMediator mediator) => _mediator = mediator;

    /// <summary>سالن‌ها و میزها با وضعیت‌شان (صفحه‌ی گارسون).</summary>
    [HttpGet("halls")]
    public async Task<IActionResult> Halls(CancellationToken ct)
        => Ok(await _mediator.Send(new GetHallsWithTablesQuery(), ct));

    /// <summary>نمایشگر آشپزخانه (KDS): رسیدهای فعال و آیتم‌هایشان.</summary>
    [HttpGet("kitchen")]
    public async Task<IActionResult> Kitchen(CancellationToken ct)
        => Ok(await _mediator.Send(new GetKitchenBoardQuery(), ct));

    /// <summary>تغییر وضعیت رسید آشپزخانه: 1=در حال آماده‌سازی، 2=آماده، 3=تحویل‌شده.</summary>
    [HttpPost("kitchen/{ticketId:int}/status/{status:int}")]
    public async Task<IActionResult> AdvanceKitchen(int ticketId, int status, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new AdvanceKitchenTicketCommand(ticketId, (SamaHesab.Domain.Enums.KitchenTicketStatus)status), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>جزئیات یک سفارش با ردیف‌ها.</summary>
    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> GetOrder(int id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetOrderQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>باز کردن سفارش (روی میز / بیرون‌بر / پیک).</summary>
    [HttpPost("orders/open")]
    public async Task<IActionResult> Open([FromBody] OpenTableCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { orderId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>افزودن یک ردیف به سفارش باز.</summary>
    [HttpPost("orders/{id:int}/items")]
    public async Task<IActionResult> AddItem(int id, [FromBody] AddOrderItemCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { OrderId = id }, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>تغییر تعداد یک ردیف سفارش (۰ یا کمتر = حذف ردیف).</summary>
    [HttpPost("orders/{id:int}/items/{itemId:int}/qty/{qty}")]
    public async Task<IActionResult> ChangeItemQty(int id, int itemId, decimal qty, CancellationToken ct)
    {
        var r = await _mediator.Send(new ChangeOrderItemQtyCommand(id, itemId, qty), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>حذف یک ردیف از سفارش (فقط ردیف ارسال‌نشده به آشپزخانه).</summary>
    [HttpDelete("orders/{id:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int id, int itemId, CancellationToken ct)
    {
        var r = await _mediator.Send(new RemoveOrderItemCommand(id, itemId), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>تنظیم یادداشت آشپزخانه‌ی یک ردیف.</summary>
    [HttpPost("orders/{id:int}/items/{itemId:int}/notes")]
    public async Task<IActionResult> SetItemNotes(int id, int itemId, [FromBody] string? notes, CancellationToken ct)
    {
        var r = await _mediator.Send(new SetOrderItemNotesCommand(id, itemId, notes), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>ارسال ردیف‌های در انتظار به آشپزخانه (ساخت رسید آشپزخانه).</summary>
    [HttpPost("orders/{id:int}/send-to-kitchen")]
    public async Task<IActionResult> SendToKitchen(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new SendOrderToKitchenCommand(id), ct);
        return r.Succeeded ? Ok(new { kitchenTicketId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>تسویه‌ی سفارش و آزادسازی میز.</summary>
    [HttpPost("orders/{id:int}/settle")]
    public async Task<IActionResult> Settle(int id, [FromBody] SettleOrderCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { OrderId = id }, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
