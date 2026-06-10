using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/[controller]")]
public class VouchersController : ControllerBase
{
    private readonly IMediator _mediator;
    public VouchersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Create a (draft) accounting voucher.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVoucherCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { voucherId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>Post (finalize) a voucher.</summary>
    [HttpPost("{id:int}/post")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new PostVoucherCommand(id), ct);
        return result.Succeeded
            ? Ok(new { posted = true })
            : BadRequest(new { message = result.ErrorMessage });
    }

    public record ReverseRequest(string Date, string? Description = null);

    /// <summary>سند برگشتی: خنثی‌کردن یک سند قطعی با جابه‌جایی بدهکار/بستانکار.</summary>
    [HttpPost("{id:int}/reverse")]
    public async Task<IActionResult> Reverse(int id, [FromBody] ReverseRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReverseVoucherCommand(id, req.Date, req.Description), ct);
        return result.Succeeded
            ? Ok(new { reversalVoucherId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>بستن سال مالی: صدور سند اختتامیه (و اختیاری سند افتتاحیه سال بعد).</summary>
    [HttpPost("close-fiscal-year")]
    public async Task<IActionResult> CloseFiscalYear([FromBody] CloseFiscalYearCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { message = result.ErrorMessage });
    }

    public record CopyRequest(string Date, string? Description = null);

    /// <summary>کپی سند به‌عنوان پیش‌نویس جدید (بهره‌وری: اسناد تکرارشونده).</summary>
    [HttpPost("{id:int}/copy")]
    public async Task<IActionResult> Copy(int id, [FromBody] CopyRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CopyVoucherCommand(id, req.Date, req.Description), ct);
        return result.Succeeded
            ? Ok(new { voucherId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>پیشنهاد هوشمند حساب‌های طرفِ مقابلِ پرتکرار برای یک حساب (auto-completion).</summary>
    [HttpGet("suggest-accounts/{accountId:int}")]
    public async Task<IActionResult> SuggestAccounts(int accountId, [FromQuery] int top, CancellationToken ct)
        => Ok(await _mediator.Send(new SuggestAccountPairsQuery(accountId, top <= 0 ? 6 : top), ct));

    // ── الگوهای سند (Voucher Templates) ──────────────────────────────────────
    /// <summary>فهرست الگوهای سند.</summary>
    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
        => Ok(await _mediator.Send(new GetVoucherTemplatesQuery(), ct));

    /// <summary>ذخیره‌ی یک الگوی سند جدید.</summary>
    [HttpPost("templates")]
    public async Task<IActionResult> SaveTemplate([FromBody] SaveVoucherTemplateCommand command, CancellationToken ct)
    {
        var r = await _mediator.Send(command, ct);
        return r.Succeeded ? Ok(new { templateId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    public record FromTemplateRequest(string Date, string? Description = null);

    /// <summary>ساخت سند پیش‌نویس از روی یک الگو.</summary>
    [HttpPost("from-template/{templateId:int}")]
    public async Task<IActionResult> FromTemplate(int templateId, [FromBody] FromTemplateRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateVoucherFromTemplateCommand(templateId, req.Date, req.Description), ct);
        return r.Succeeded ? Ok(new { voucherId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    // ── اسناد تکرارشونده (Recurring Vouchers) ────────────────────────────────
    /// <summary>فهرست اسناد تکرارشونده.</summary>
    [HttpGet("recurring")]
    public async Task<IActionResult> Recurring(CancellationToken ct)
        => Ok(await _mediator.Send(new GetRecurringVouchersQuery(), ct));

    /// <summary>تعریف یک سند تکرارشونده (الگو + دوره + تاریخ شروع).</summary>
    [HttpPost("recurring")]
    public async Task<IActionResult> SaveRecurring([FromBody] SaveRecurringVoucherCommand command, CancellationToken ct)
    {
        var r = await _mediator.Send(command, ct);
        return r.Succeeded ? Ok(new { recurringId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    public record RunRecurringRequest(string Today);

    /// <summary>اجرای موتور تولید: ساخت اسناد پیش‌نویس برای همه‌ی موارد سررسیدشده تا «امروز».</summary>
    [HttpPost("recurring/run")]
    public async Task<IActionResult> RunRecurring([FromBody] RunRecurringRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new GenerateDueRecurringVouchersCommand(req.Today), ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }
}
