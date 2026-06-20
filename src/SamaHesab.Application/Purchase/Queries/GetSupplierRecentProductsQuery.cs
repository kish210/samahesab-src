using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Queries;

/// <summary>
/// UX-PURCHASE-1 — کالاهایی که از این تأمین‌کننده اخیراً خریداری شده (چیپ‌های «افزودنِ سریع» در فاکتور خرید).
/// قرینهٔ <c>GetCustomerRecentProductsQuery</c> در فروش — گردشِ خریدِ مجدد.
/// </summary>
public record GetSupplierRecentProductsQuery(int SupplierId, int Take = 8)
    : IRequest<List<SupplierRecentProductDto>>;

public record SupplierRecentProductDto(
    int ProductId, string Code, string Name, string? Barcode, decimal Price, decimal TaxRate);

public class GetSupplierRecentProductsQueryHandler
    : IRequestHandler<GetSupplierRecentProductsQuery, List<SupplierRecentProductDto>>
{
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IRepository<PurchaseInvoiceItem> _items;
    private readonly IRepository<Product> _products;
    private readonly ICurrentUserService _currentUser;

    public GetSupplierRecentProductsQueryHandler(IRepository<PurchaseInvoice> invoices,
        IRepository<PurchaseInvoiceItem> items, IRepository<Product> products, ICurrentUserService currentUser)
    { _invoices = invoices; _items = items; _products = products; _currentUser = currentUser; }

    public async Task<List<SupplierRecentProductDto>> Handle(GetSupplierRecentProductsQuery req, CancellationToken ct)
    {
        if (req.SupplierId <= 0) return new();
        var companyId = _currentUser.CompanyId ?? 1;

        var purchases = await _invoices.FindAsync(
            i => i.CompanyId == companyId
                 && i.SupplierId == req.SupplierId
                 && i.InvoiceType == "خرید"
                 && i.StatusCode == "قطعی", ct);
        if (purchases.Count == 0) return new();
        var dateOf = purchases.ToDictionary(i => i.Id, i => i.InvoiceDate);

        var lines = await _items.FindAsync(it => dateOf.Keys.Contains(it.InvoiceId), ct);
        var recentProductIds = lines
            .GroupBy(it => it.ProductId)
            .Select(g => new { ProductId = g.Key, LastDate = g.Max(it => dateOf[it.InvoiceId]) })
            .OrderByDescending(x => x.LastDate, StringComparer.Ordinal)
            .Take(req.Take)
            .Select(x => x.ProductId)
            .ToList();
        if (recentProductIds.Count == 0) return new();

        var prods = (await _products.FindAsync(
                p => p.CompanyId == companyId && p.IsActive && recentProductIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id);

        return recentProductIds
            .Where(id => prods.ContainsKey(id))
            .Select(id => prods[id])
            // برای خرید، «فی» را قیمتِ خرید (PurchasePrice) قرار می‌دهیم نه فروش.
            .Select(p => new SupplierRecentProductDto(p.Id, p.Code, p.Name, p.Barcode, p.PurchasePrice, p.TaxRate))
            .ToList();
    }
}
