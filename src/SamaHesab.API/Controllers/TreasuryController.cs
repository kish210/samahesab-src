using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Treasury.Commands;

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
}
