using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Treasury.Commands;
using SamaHesab.Application.Treasury.Queries;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/[controller]")]
public class TreasuryController : ControllerBase
{
    private readonly IMediator _mediator;
    public TreasuryController(IMediator mediator) => _mediator = mediator;

    /// <summary>دریافت وجه از مشتری.</summary>
    [HttpPost("receipts")]
    public async Task<IActionResult> Receipt([FromBody] CreateReceiptCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { voucherId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>پرداخت وجه به تأمین‌کننده.</summary>
    [HttpPost("payments")]
    public async Task<IActionResult> Payment([FromBody] CreatePaymentCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { voucherId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>سندِ تسویهٔ بین‌شعبه (انتقالِ وجه بین دو شعبه — دو سندِ قطعیِ متوازن با حساب فی‌مابین).</summary>
    [HttpPost("interbranch-transfer")]
    public async Task<IActionResult> InterBranchTransfer([FromBody] CreateInterBranchTransferCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded
            ? Ok(new { fromVoucherId = r.Value!.FromVoucherId, toVoucherId = r.Value.ToVoucherId, reference = r.Value.Reference })
            : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>فهرست دریافتنی‌ها (مشتریان بدهکار، مرتب بر مبلغ).</summary>
    [HttpGet("receivables")]
    public async Task<IActionResult> Receivables([FromQuery] int max, CancellationToken ct)
        => Ok(await _mediator.Send(new GetReceivablesQuery(max <= 0 ? 200 : max), ct));

    /// <summary>فهرست پرداختنی‌ها (تأمین‌کنندگان بستانکار، مرتب بر مبلغ).</summary>
    [HttpGet("payables")]
    public async Task<IActionResult> Payables([FromQuery] int max, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayablesQuery(max <= 0 ? 200 : max), ct));
}
