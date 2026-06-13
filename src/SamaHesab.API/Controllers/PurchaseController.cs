using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Purchase.Commands;
using SamaHesab.Application.Purchase.Queries;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/purchase")]
public class PurchaseController : ControllerBase
{
    private readonly IMediator _mediator;
    public PurchaseController(IMediator mediator) => _mediator = mediator;

    /// <summary>Create a purchase invoice (increases stock + posts the automatic voucher).</summary>
    [HttpPost("invoices")]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseInvoiceCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { invoiceId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>مرجوعی خرید (C2-C): خروج کالا از انبار (بازگشت به تأمین‌کننده) + سند حسابداری معکوس.</summary>
    [HttpPost("returns")]
    public async Task<IActionResult> Return([FromBody] CreatePurchaseReturnCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { returnInvoiceId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>فهرست سفارش‌های خرید (اختیاری: فیلتر وضعیت).</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPurchaseOrdersQuery(status), ct));

    /// <summary>ساخت خودکار سفارش خرید از پیشنهادهای نقطه‌ی سفارش.</summary>
    [HttpPost("orders/from-reorder")]
    public async Task<IActionResult> FromReorder([FromBody] CreatePurchaseOrderFromReorderCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { orderId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
