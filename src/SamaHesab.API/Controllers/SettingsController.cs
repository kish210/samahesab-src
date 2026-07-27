using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Settings;

namespace SamaHesab.API.Controllers;

/// <summary>تنظیماتِ شرکتی (نام/کدِ ملی/کدِ اقتصادی/تلفن/آدرس) — برایِ سربرگِ چاپیِ فاکتور/رسید در وب.</summary>
[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    public SettingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("company")]
    public async Task<IActionResult> Company(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCompanySettingsQuery(), ct));
}
