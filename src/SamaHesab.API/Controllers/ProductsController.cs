using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _products;
    private readonly ICurrentUserService _currentUser;

    public ProductsController(IProductRepository products, ICurrentUserService currentUser)
    {
        _products = products;
        _currentUser = currentUser;
    }

    /// <summary>Search products by code / barcode / name (empty = all).</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _products.SearchAsync(companyId, q ?? string.Empty, ct);
        return Ok(list.Select(p => new
        {
            p.Id, p.Code, p.Name, p.Barcode,
            p.PurchasePrice, p.SalePrice, p.WholesalePrice, p.MinStock, p.TaxRate, p.IsActive
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var p = await _products.GetByIdAsync(id, ct);
        if (p == null) return NotFound();
        return Ok(new { p.Id, p.Code, p.Name, p.Barcode, p.PurchasePrice, p.SalePrice, p.MinStock, p.IsActive });
    }
}
