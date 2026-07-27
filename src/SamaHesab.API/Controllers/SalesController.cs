using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Application.Sales.Queries;
using SamaHesab.Domain.Enums;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    public SalesController(IMediator mediator, ICurrentUserService currentUser, IPersianCalendarService calendar)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    /// <summary>فهرستِ فاکتورهای فروش (با نامِ مشتری و وضعیت) — الگوی API-only.</summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> List([FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int? customerId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSalesInvoicesQuery(from, to, status, search, customerId), ct));

    /// <summary>یک فاکتورِ فروش با اقلامش — برایِ صفحهٔ نمایش/چاپ.</summary>
    [HttpGet("invoices/{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetSalesInvoiceByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Create a sales invoice (reduces stock + posts the automatic voucher).</summary>
    [HttpPost("invoices")]
    public async Task<IActionResult> Create([FromBody] CreateSalesInvoiceCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { invoiceId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>مرجوعی فروش (کار #۳۲): بازگشت کالا به انبار + سند حسابداری معکوس.</summary>
    [HttpPost("returns")]
    public async Task<IActionResult> Return([FromBody] CreateSalesReturnCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { returnInvoiceId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    public record PosSaleItem(int ProductId, decimal Quantity, decimal UnitPrice, decimal DiscountPct = 0, decimal TaxPct = 0);
    public record PosSaleRequest(List<PosSaleItem> Items, decimal Paid = 0, string PaymentMethod = "نقدی",
        int CustomerId = 1, int WarehouseId = 1, decimal Discount = 0, decimal OtherCosts = 0, string? Description = null);

    /// <summary>
    /// Simplified POS / restaurant checkout: the kiosk sends only items + payment and the
    /// server builds the full sales invoice (date, branch, type) — so touch clients never
    /// touch the database directly, only this API.
    /// </summary>
    [HttpPost("pos")]
    public async Task<IActionResult> PosSale([FromBody] PosSaleRequest req, CancellationToken ct)
    {
        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { message = "سبد خالی است." });

        // U-ACCT-1.7: پیش‌تر FiscalYearId=۱ هاردکد بود.
        var fiscalYearId = await _mediator.Send(
            new SamaHesab.Application.Accounting.Dimensions.GetActiveFiscalYearQuery(), ct);
        var cmd = new CreateSalesInvoiceCommand(
            BranchId: _currentUser.BranchId ?? 1,
            FiscalYearId: fiscalYearId,
            InvoiceDate: _calendar.GetCurrentPersianDate(),
            CustomerId: req.CustomerId,
            WarehouseId: req.WarehouseId,
            InvoiceType: InvoiceType.Sale,
            PriceLevel: "خرده",
            SalesRepId: null,
            DueDate: null,
            Description: req.Description ?? "فروش صندوق (POS)",
            Shipping: 0,
            OtherCosts: req.OtherCosts,
            Items: req.Items.Select(i => new SalesInvoiceItemDto(
                i.ProductId, i.Quantity, i.UnitPrice, i.DiscountPct, i.TaxPct, null, null, null)).ToList(),
            InvoiceDiscount: req.Discount,
            PaidAmount: req.Paid,
            PaymentMethod: req.PaymentMethod,
            CommissionPercent: 0);

        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { invoiceId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

}
