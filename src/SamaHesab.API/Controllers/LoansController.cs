using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-LOAN — تسهیلاتِ مالی/وام (هم‌راستا با «تسهیلات مالی»یِ راهکاران).
/// </summary>
[ApiController]
[Authorize]
[Route("api/loans")]
public class LoansController : ControllerBase
{
    private readonly IMediator _mediator;
    public LoansController(IMediator mediator) => _mediator = mediator;

    /// <summary>فهرستِ وام‌ها با مانده و مبلغِ قسطِ محاسبه‌شده.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new GetLoansQuery(), ct));

    /// <summary>جدولِ کاملِ اقساطِ یک وام.</summary>
    [HttpGet("{id:int}/schedule")]
    public async Task<IActionResult> Schedule(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetLoanScheduleQuery(id), ct));

    /// <summary>ثبتِ وام (سندِ دریافت هم صادر می‌شود).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoanCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>پرداختِ قسطِ بعدی و صدورِ سندِ پرداخت.</summary>
    [HttpPost("{id:int}/installments/{index:int}/pay")]
    public async Task<IActionResult> Pay(int id, int index, [FromBody] PayLoanInstallmentCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { Id = id, InstallmentIndex = index }, ct);
        return r.Succeeded ? Ok(new { voucherId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }
}
