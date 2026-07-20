using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Purchase.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly IRepository<Party> _suppliers;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public SuppliersController(IRepository<Party> suppliers, ICurrentUserService currentUser, IMediator mediator)
    {
        _suppliers = suppliers;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    /// <summary>صورت‌حساب تأمین‌کننده (تراکنش‌ها با ماندهٔ غلتان) — کارِ ۶.</summary>
    [HttpGet("{id:int}/statement")]
    public async Task<IActionResult> Statement(int id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSupplierStatementQuery(id, from, to), ct);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSuppliersQuery(search), ct));

    /// <summary>
    /// U-WEB-DEACTIVATE — غیرفعال/فعال‌سازیِ (حذفِ نرم) تأمین‌کننده. حذفِ واقعی عمداً نیست:
    /// فاکتورهایِ خریدِ تاریخی به همین Party ارجاع می‌دهند.
    /// </summary>
    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new SamaHesab.Application.CRM.Commands.SetPartyActiveCommand(id, false), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new SamaHesab.Application.CRM.Commands.SetPartyActiveCommand(id, true), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
