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
    private readonly IExcelExportService _excel;

    public ProductsController(IProductRepository products, ICurrentUserService currentUser, IExcelExportService excel)
    {
        _products = products;
        _currentUser = currentUser;
        _excel = excel;
    }

    /// <summary>Export the product list to an Excel (.xlsx) file.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? q, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _products.SearchAsync(companyId, q ?? string.Empty, ct);
        var headers = new[] { "کد", "بارکد", "نام کالا", "قیمت خرید", "قیمت فروش", "حداقل موجودی", "وضعیت" };
        var rows = list.Select(p => (IReadOnlyList<object?>)new object?[]
        {
            p.Code, p.Barcode, p.Name, p.PurchasePrice, p.SalePrice, p.MinStock, p.IsActive
        }).ToList();
        var bytes = _excel.Export("کالاها", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "products.xlsx");
    }

    /// <summary>Product groups (categories) for the restaurant/POS category tiles.</summary>
    [HttpGet("groups")]
    public async Task<IActionResult> Groups([FromServices] SamaHesab.Domain.Interfaces.Repositories.IRepository<SamaHesab.Domain.Entities.Inventory.ProductGroup> groups, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        try
        {
            var list = await groups.FindAsync(g => g.CompanyId == companyId, ct);
            return Ok(list.Select(g => new { g.Id, g.Name }));
        }
        catch { return Ok(Array.Empty<object>()); }
    }

    /// <summary>Search products by code / barcode / name (empty = all).</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _products.SearchAsync(companyId, q ?? string.Empty, ct);
        return Ok(list.Select(p => new
        {
            p.GroupId,
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
