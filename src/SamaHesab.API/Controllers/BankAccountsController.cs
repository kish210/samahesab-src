using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>حساب‌های بانکی — الگوی API-only.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BankAccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    public BankAccountsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await _mediator.Send(new GetBankAccountsQuery(activeOnly), ct));
}
