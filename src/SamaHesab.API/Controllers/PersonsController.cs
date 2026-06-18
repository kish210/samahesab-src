using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.CRM.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// اشخاص (طرف‌حساب) — فهرستِ یکپارچهٔ مشتری + تأمین‌کننده.
/// الگوی مرجعِ معماریِ API-only: کلاینت‌ها از این endpoint می‌خوانند، نه مستقیم از DB.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PersonsController : ControllerBase
{
    private readonly IMediator _mediator;
    public PersonsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int? role, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPersonsQuery(search, role), ct));
}
