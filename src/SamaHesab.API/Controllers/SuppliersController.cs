using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Purchase.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly IRepository<Supplier> _suppliers;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public SuppliersController(IRepository<Supplier> suppliers, ICurrentUserService currentUser, IMediator mediator)
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
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _suppliers.FindAsync(s => s.CompanyId == companyId, ct);
        return Ok(list.Select(s => new
        {
            s.Id, s.Code, Name = s.FullName, s.Mobile, s.Balance, s.IsActive
        }));
    }
}
