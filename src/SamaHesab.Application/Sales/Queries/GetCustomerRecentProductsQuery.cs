using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Queries;

/// <summary>
/// UX-SALES-2 — کالاهایی که این مشتری اخیراً خریده (برای چیپ‌های «افزودنِ سریع» در فاکتور فروش).
/// گردشِ سفارشِ مجدد: فروشنده با یک کلیک کالای پرتکرارِ مشتری را دوباره می‌افزاید.
/// </summary>
public record GetCustomerRecentProductsQuery(int CustomerId, int Take = 8)
    : IRequest<List<CustomerRecentProductDto>>;

public record CustomerRecentProductDto(
    int ProductId, string Code, string Name, string? Barcode, decimal Price, decimal TaxRate);

public class GetCustomerRecentProductsQueryHandler
    : IRequestHandler<GetCustomerRecentProductsQuery, List<CustomerRecentProductDto>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceItem> _items;
    private readonly IRepository<Product> _products;
    private readonly ICurrentUserService _currentUser;

    public GetCustomerRecentProductsQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<SalesInvoiceItem> items, IRepository<Product> products, ICurrentUserService currentUser)
    { _invoices = invoices; _items = items; _products = products; _currentUser = currentUser; }

    public async Task<List<CustomerRecentProductDto>> Handle(GetCustomerRecentProductsQuery req, CancellationToken ct)
    {
        if (req.CustomerId <= 0) return new();
        var companyId = _currentUser.CompanyId ?? 1;

        // فاکتورهای فروشِ معتبرِ همین مشتری → نگاشتِ Id→تاریخ.
        var sales = await _invoices.FindAsync(
            i => i.CompanyId == companyId
                 && i.CustomerId == req.CustomerId
                 && i.InvoiceType == InvoiceType.Sale
                 && i.Status != InvoiceStatus.Draft
                 && i.Status != InvoiceStatus.Cancelled, ct);
        if (sales.Count == 0) return new();
        var dateOf = sales.ToDictionary(i => i.Id, i => i.InvoiceDate);

        // ردیف‌های این فاکتورها → آخرین تاریخِ خرید به‌ازای هر کالا، مرتب نزولی.
        var lines = await _items.FindAsync(it => dateOf.Keys.Contains(it.InvoiceId), ct);
        var recentProductIds = lines
            .GroupBy(it => it.ProductId)
            .Select(g => new { ProductId = g.Key, LastDate = g.Max(it => dateOf[it.InvoiceId]) })
            .OrderByDescending(x => x.LastDate, StringComparer.Ordinal)
            .Take(req.Take)
            .Select(x => x.ProductId)
            .ToList();
        if (recentProductIds.Count == 0) return new();

        // فقط کالاهای فعال (غیرفعال‌ها در فهرستِ افزودنِ سریع نیایند). ترتیبِ تازگی حفظ شود.
        var prods = (await _products.FindAsync(
                p => p.CompanyId == companyId && p.IsActive && recentProductIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id);

        return recentProductIds
            .Where(id => prods.ContainsKey(id))
            .Select(id => prods[id])
            .Select(p => new CustomerRecentProductDto(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate))
            .ToList();
    }
}
