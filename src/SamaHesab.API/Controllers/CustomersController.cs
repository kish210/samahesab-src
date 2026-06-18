using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IRepository<Customer> _customers;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public CustomersController(IRepository<Customer> customers, ICurrentUserService currentUser, IMediator mediator)
    {
        _customers = customers;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    /// <summary>وضعیت اعتبار مشتری: مانده، سقف، اعتبار باقی‌مانده (کار #۳۷).</summary>
    [HttpGet("{id:int}/credit")]
    public async Task<IActionResult> Credit(int id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetCustomerCreditQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Customer account statement (transactions with running balance).</summary>
    [HttpGet("{id:int}/statement")]
    public async Task<IActionResult> Statement(int id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCustomerStatementQuery(id, from, to), ct);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct)
        => Ok(await _mediator.Send(new GetCustomersQuery(search), ct));

    /// <summary>کارت ۳۶۰° مشتری (شناسنامه + چکِ در جریان) — الگوی API-only.</summary>
    [HttpGet("{id:int}/card")]
    public async Task<IActionResult> Card(int id, CancellationToken ct)
        => (await _mediator.Send(new GetCustomerCardQuery(id), ct)) is { } dto ? Ok(dto) : NotFound();

    /// <summary>ساختِ مشتری — الگوی API-only.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SamaHesab.Application.CRM.Commands.CreateCustomerCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }
}
