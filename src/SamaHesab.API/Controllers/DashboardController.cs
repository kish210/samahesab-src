using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IProductRepository _products;
    private readonly IRepository<Party> _customers;
    private readonly IRepository<Party> _suppliers;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public DashboardController(IProductRepository products, IRepository<Party> customers,
        IRepository<Party> suppliers, ICurrentUserService currentUser, IMediator mediator)
    {
        _products = products; _customers = customers; _suppliers = suppliers; _currentUser = currentUser; _mediator = mediator;
    }

    /// <summary>داشبوردِ کاملِ عملیاتی (KPI + فهرست‌ها) — الگوی API-only.</summary>
    [HttpGet("full")]
    public async Task<IActionResult> Full([FromQuery] string today, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDashboardQuery(today), ct));

    /// <summary>Operational KPI summary (counts + outstanding balances).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var products = await _products.FindAsync(p => p.CompanyId == companyId, ct);
        var customers = await _customers.FindAsync(c => c.CompanyId == companyId && c.IsCustomer, ct);
        var suppliers = await _suppliers.FindAsync(s => s.CompanyId == companyId && s.IsSupplier, ct);

        return Ok(new
        {
            totalProducts = products.Count,
            totalCustomers = customers.Count,
            lowStock = products.Count(p => p.MinStock > 0),
            receivable = customers.Where(c => c.Balance > 0).Sum(c => c.Balance),
            payable = suppliers.Where(s => s.Balance > 0).Sum(s => s.Balance)
        });
    }
}
