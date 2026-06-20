using MediatR;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Application.Sales.Queries;

namespace SamaHesab.WPF.Services.Search;

// ── providerهای lane لپ‌تاپ (C2) برای جست‌وجوی سراسریِ CC-1 ──
// روی همان contractِ IGlobalSearchProvider که C1 ساخته. هر provider در DI-scopeِ مستقلِ
// GlobalSearchService صدا زده می‌شود و ترتیبی اجرا می‌شود (DbContextِ مشترکِ همان scope).

/// <summary>C2 — جست‌وجوی کالا بر اساسِ نام/کد/بارکد (سرویس‌محور: GetProductsQuery فیلتر می‌کند).</summary>
public sealed class ProductsSearchProvider : IGlobalSearchProvider
{
    private readonly IMediator _mediator;
    public ProductsSearchProvider(IMediator mediator) => _mediator = mediator;

    public async Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var rows = await _mediator.Send(new GetProductsQuery(term), ct);
        return rows
            .Select(p => new GlobalSearchResult(
                "کالاها", "📦", p.Name,
                $"کد {p.Code} · فی {p.SalePrice:N0}",
                "ProductCard", p.Id))
            .ToList();
    }
}

/// <summary>C2 — جست‌وجوی مشتری بر اساسِ نام/کد/موبایل → کارت مشتری (Param=Id).</summary>
public sealed class CustomersSearchProvider : IGlobalSearchProvider
{
    private readonly IMediator _mediator;
    public CustomersSearchProvider(IMediator mediator) => _mediator = mediator;

    public async Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var rows = await _mediator.Send(new GetCustomersQuery(term), ct);
        return rows
            .Select(c => new GlobalSearchResult(
                "مشتریان", "👤", c.Name,
                string.IsNullOrWhiteSpace(c.Mobile) ? $"کد {c.Code}" : $"کد {c.Code} · {c.Mobile}",
                "CustomerCard", c.Id))
            .ToList();
    }
}

/// <summary>C2 — جست‌وجوی فاکتورِ فروش بر اساسِ شماره/مشتری → لیستِ فروش.</summary>
public sealed class SalesInvoicesSearchProvider : IGlobalSearchProvider
{
    private readonly IMediator _mediator;
    public SalesInvoicesSearchProvider(IMediator mediator) => _mediator = mediator;

    public async Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var rows = await _mediator.Send(new GetSalesInvoicesQuery(Search: term), ct);
        return rows
            .Select(i => new GlobalSearchResult(
                "فاکتورها", "🧾", $"فاکتور {i.Number}",
                $"{i.CustomerName} · {i.Total:N0} · {i.Status}",
                "SalesInvoiceList"))
            .ToList();
    }
}
