using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>مدیریت چک — تابلوی سررسید + تغییر وضعیت (P7، تکمیل پوشش API).</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/[controller]")]
public class ChequesController : ControllerBase
{
    private readonly IMediator _mediator;
    public ChequesController(IMediator mediator) => _mediator = mediator;

    /// <summary>تابلوی چک‌های در جریان مرتب بر سررسید (با علامت سررسیدگذشته/امروز).</summary>
    [HttpGet("board")]
    public async Task<IActionResult> Board([FromQuery] string today, CancellationToken ct)
        => Ok(await _mediator.Send(new GetChequeBoardQuery(today), ct));

    /// <summary>فهرستِ کاملِ چک‌ها (اختیاری: فیلترِ طرف‌حساب) — الگوی API-only.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? partyId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetChequesQuery(partyId), ct));

    /// <summary>ثبتِ چکِ دریافتی/پرداختیِ نو — الگوی API-only (Commandِ ازقبل‌موجود، بدونِ اندپوینتِ وب تا این‌جا).</summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterChequeCommand command, CancellationToken ct)
    {
        var r = await _mediator.Send(command, ct);
        return r.Succeeded ? Ok(new { chequeId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    public record ChangeStatusRequest(ChequeAction Action, string Date, string? ReturnReason = null);

    /// <summary>تغییر وضعیت چک: وصول (سند خودکار)/برگشت/واگذاری.</summary>
    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new ChangeChequeStatusCommand(id, req.Action, req.Date, req.ReturnReason), ct);
        return r.Succeeded ? Ok(new { chequeId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }
}
