using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Commands;

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
}
