using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Interfaces;
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

    public CustomersController(IRepository<Customer> customers, ICurrentUserService currentUser)
    {
        _customers = customers;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _customers.FindAsync(c => c.CompanyId == companyId, ct);
        return Ok(list.Select(c => new
        {
            c.Id, c.Code, Name = c.FullName, c.Mobile, c.PriceLevel, c.Balance, c.IsActive
        }));
    }
}
