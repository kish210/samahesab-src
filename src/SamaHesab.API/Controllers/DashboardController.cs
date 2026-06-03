using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Supplier> _suppliers;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(IProductRepository products, IRepository<Customer> customers,
        IRepository<Supplier> suppliers, ICurrentUserService currentUser)
    {
        _products = products; _customers = customers; _suppliers = suppliers; _currentUser = currentUser;
    }

    /// <summary>Operational KPI summary (counts + outstanding balances).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var products = await _products.FindAsync(p => p.CompanyId == companyId, ct);
        var customers = await _customers.FindAsync(c => c.CompanyId == companyId, ct);
        var suppliers = await _suppliers.FindAsync(s => s.CompanyId == companyId, ct);

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
