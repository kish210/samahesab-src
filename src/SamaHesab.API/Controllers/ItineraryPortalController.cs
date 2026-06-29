using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Modules.Tourism.Application.Itinerary;

namespace SamaHesab.API.Controllers;

/// <summary>
/// MOD-TIT — APIِ پنلِ مهمانِ برنامه‌ریزیِ اقامتی. [AllowAnonymous]: توکنِ یکتای GUID خودش کلیدِ
/// دسترسی است (مهمان لاگین ندارد). در درخواستِ ناشناس فیلترِ multi-tenant غیرفعال است، پس جستجو
/// بر توکن کار می‌کند؛ چون توکن غیرقابلِ‌حدس است، مهمان فقط برنامهٔ خودش را می‌بیند/ویرایش می‌کند.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/itinerary")]
public class ItineraryPortalController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItineraryPortalController(IMediator mediator) => _mediator = mediator;

    /// <summary>برنامهٔ مهمان را با توکن برمی‌گرداند (مشاهده در پنل).</summary>
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetGuestItineraryQuery(token), ct);
        return res.Succeeded ? Ok(res.Value) : NotFound(new { error = res.ErrorMessage });
    }

    /// <summary>ویرایش (حذفِ اقلام) و/یا تأییدِ نهاییِ برنامه توسطِ مهمان.</summary>
    [HttpPost("{token}/submit")]
    public async Task<IActionResult> Submit(string token, [FromBody] SubmitGuestItineraryRequest body, CancellationToken ct)
    {
        var res = await _mediator.Send(new SubmitGuestItineraryCommand(
            token, body.RemovedStopIds ?? new List<int>(), body.Confirm, body.Notes), ct);
        return res.Succeeded ? Ok(new { ok = true }) : BadRequest(new { error = res.ErrorMessage });
    }

    public record SubmitGuestItineraryRequest(List<int>? RemovedStopIds, bool Confirm, string? Notes);
}
