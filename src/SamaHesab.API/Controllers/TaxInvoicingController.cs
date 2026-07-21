using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Modules.TaxInvoicing.Application.Commands;
using SamaHesab.Modules.TaxInvoicing.Application.Queries;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.API.Controllers;

/// <summary>
/// ماژولِ اختیاریِ «سامانهٔ مودیان و پایانه‌هایِ فروشگاهی» — لایهٔ APIِ کلاینتِ وب (پیش‌تر فقط
/// دسکتاپ داشت؛ منطق در Application از قبل مشترک بود). طبقِ CLAUDE.md همهٔ منطق در CQRS است،
/// کنترلر فقط پل به مدیاتور است.
/// </summary>
[ApiController]
[Authorize]
[Route("api/tax-invoicing")]
public class TaxInvoicingController : ControllerBase
{
    private readonly IMediator _mediator;
    public TaxInvoicingController(IMediator mediator) => _mediator = mediator;

    /// <summary>تنظیماتِ سامانهٔ مودیانِ شرکتِ جاری.</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _mediator.Send(new GetModianSettingsQuery(), ct));

    /// <summary>ذخیرهٔ تنظیماتِ سامانهٔ مودیان.</summary>
    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] SaveModianSettingsCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>فهرستِ صورتحساب‌هایِ الکترونیکی (جدیدترین اول).</summary>
    [HttpGet("submissions")]
    public async Task<IActionResult> GetSubmissions([FromQuery] SubmissionStatus? status, [FromQuery] int maxRows, CancellationToken ct)
        => Ok(await _mediator.Send(new GetElectronicInvoiceSubmissionsQuery(status, maxRows <= 0 ? 500 : maxRows), ct));

    /// <summary>ارسال/تلاشِ مجددِ صورتحسابِ الکترونیکیِ یک فاکتورِ فروشِ مشخص.</summary>
    [HttpPost("invoices/{salesInvoiceId:int}/send")]
    public async Task<IActionResult> SendInvoice(int salesInvoiceId, CancellationToken ct)
    {
        var r = await _mediator.Send(new SendInvoiceToTaxAuthorityCommand(salesInvoiceId), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>ارسالِ دستیِ کلِ صفِ در‌انتظار/خطا.</summary>
    [HttpPost("retry-pending")]
    public async Task<IActionResult> RetryPending(CancellationToken ct)
    {
        var r = await _mediator.Send(new RetryPendingElectronicInvoicesCommand(), ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>فهرستِ کالاها به‌همراهِ نگاشتِ کدِ کالاییِ مودیان — برایِ صفحهٔ «کدهایِ کالایی».</summary>
    [HttpGet("item-codes")]
    public async Task<IActionResult> GetItemCodes([FromQuery] string? search, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTaxItemCodesQuery(search), ct));

    /// <summary>ذخیرهٔ نگاشتِ کدِ کالاییِ مودیان برایِ یک کالا.</summary>
    [HttpPost("item-codes")]
    public async Task<IActionResult> SaveItemCode([FromBody] SaveTaxItemCodeCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
