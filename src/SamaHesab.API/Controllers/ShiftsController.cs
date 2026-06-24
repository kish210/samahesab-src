using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Modules.POS.Application;

namespace SamaHesab.API.Controllers;

/// <summary>کار #۳۰ — شیفت/صندوق POS: باز/بستن صندوق، ثبت فروش، و شیفت باز جاری (Z-report).</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ShiftsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ShiftsController(IMediator mediator) => _mediator = mediator;

    public record OpenRequest(decimal OpeningFloat);
    public record SaleRequest(decimal Amount, bool IsCash);
    public record CloseRequest(decimal CountedCash, string? Notes = null);

    /// <summary>شیفت باز جاری کاربر (یا خالی).</summary>
    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetOpenShiftQuery(), ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new OpenShiftCommand(req.OpeningFloat), ct);
        return r.Succeeded ? Ok(new { shiftId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("record-sale")]
    public async Task<IActionResult> RecordSale([FromBody] SaleRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new RecordShiftSaleCommand(req.Amount, req.IsCash), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new CloseShiftCommand(req.CountedCash, req.Notes), ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }
}
