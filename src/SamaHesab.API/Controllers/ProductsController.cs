using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
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
    private readonly IMediator _mediator;

    public ProductsController(IProductRepository products, ICurrentUserService currentUser,
        IExcelExportService excel, IMediator mediator)
    {
        _products = products;
        _currentUser = currentUser;
        _excel = excel;
        _mediator = mediator;
    }

    /// <summary>فهرستِ کاملِ کالاها برای صفحهٔ مدیریتِ کالا (با هشدارِ کسری) — الگوی API-only.</summary>
    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProductsQuery(search), ct));

    /// <summary>کارت کالا (۳۶۰°): مشخصات/قیمت/کنترل + موجودیِ چنداِنباره — الگوی API-only.</summary>
    [HttpGet("{id:int}/card")]
    public async Task<IActionResult> Card(int id, CancellationToken ct)
        => (await _mediator.Send(new GetProductCardQuery(id), ct)) is { } dto ? Ok(dto) : NotFound();

    /// <summary>کاردکسِ کالا (گردشِ ورود/خروج/موجودی) — از قبل در Application موجود بود
    /// (`GetKardexQuery`) ولی هیچ اندپوینتی صدایش نمی‌زد.</summary>
    [HttpGet("{id:int}/kardex")]
    public async Task<IActionResult> Kardex(int id, [FromQuery] int? warehouseId, [FromQuery] string? fromDate, [FromQuery] string? toDate, CancellationToken ct)
        => Ok(await _mediator.Send(new GetKardexQuery(id, warehouseId, fromDate, toDate), ct));

    /// <summary>غیرفعال‌سازیِ (حذفِ نرمِ) کالا.</summary>
    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeactivateProductCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>U-WEB-DEACTIVATE — بازفعال‌سازیِ کالایِ غیرفعال‌شده.</summary>
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new SamaHesab.Application.Inventory.Commands.ActivateProductCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>
    /// U-WEB-CRUD — ساختِ کالا. `CreateProductCommand` از قبل در Application بود ولی مسیرِ APIای
    /// نداشت ⇒ کلاینتِ وب نمی‌توانست کالا اضافه کند.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SamaHesab.Application.Inventory.Commands.CreateProductCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>U-WEB-CRUD — ویرایشِ کالا (Idِ مسیر مرجع است، نه بدنه).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,
        [FromBody] SamaHesab.Application.Inventory.Commands.UpdateProductCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { ProductId = id }, ct);
        return r.Succeeded ? Ok(new { id }) : BadRequest(new { message = r.ErrorMessage });
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
