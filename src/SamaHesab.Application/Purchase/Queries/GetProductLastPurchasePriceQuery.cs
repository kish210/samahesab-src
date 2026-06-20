using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Queries;

/// <summary>
/// UX-PURCHASE-1 — آخرین قیمتِ خریدِ یک کالا (هینتِ تاریخچهٔ قیمت در فاکتور خرید).
/// «کلی» = آخرین خریدِ این کالا از هر تأمین‌کننده؛ «این تأمین‌کننده» = آخرین خرید از تأمین‌کنندهٔ جاری.
/// </summary>
public record GetProductLastPurchasePriceQuery(int ProductId, int? SupplierId)
    : IRequest<ProductLastPurchasePriceDto>;

public record ProductLastPurchasePriceDto(
    decimal? LastPrice, string? LastDate,
    decimal? LastPriceForSupplier, string? LastDateForSupplier);

public class GetProductLastPurchasePriceQueryHandler
    : IRequestHandler<GetProductLastPurchasePriceQuery, ProductLastPurchasePriceDto>
{
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IRepository<PurchaseInvoiceItem> _items;
    private readonly ICurrentUserService _currentUser;

    public GetProductLastPurchasePriceQueryHandler(IRepository<PurchaseInvoice> invoices,
        IRepository<PurchaseInvoiceItem> items, ICurrentUserService currentUser)
    { _invoices = invoices; _items = items; _currentUser = currentUser; }

    public async Task<ProductLastPurchasePriceDto> Handle(GetProductLastPurchasePriceQuery req, CancellationToken ct)
    {
        if (req.ProductId <= 0) return new ProductLastPurchasePriceDto(null, null, null, null);
        var companyId = _currentUser.CompanyId ?? 1;

        // فاکتورهای خریدِ قطعی (نه پیش‌نویس/برگشت خرید).
        var purchases = await _invoices.FindAsync(
            i => i.CompanyId == companyId
                 && i.InvoiceType == "خرید"
                 && i.StatusCode == "قطعی", ct);
        var dateOf = purchases.ToDictionary(i => i.Id, i => i.InvoiceDate);
        var supOf = purchases.ToDictionary(i => i.Id, i => i.SupplierId);

        var lines = (await _items.FindAsync(it => it.ProductId == req.ProductId, ct))
            .Where(it => dateOf.ContainsKey(it.InvoiceId))
            .Select(it => new { it.UnitPrice, Date = dateOf[it.InvoiceId], Supplier = supOf[it.InvoiceId] })
            .OrderByDescending(x => x.Date, StringComparer.Ordinal)
            .ToList();

        var last = lines.FirstOrDefault();
        var lastForSupplier = req.SupplierId is int sid && sid > 0
            ? lines.FirstOrDefault(x => x.Supplier == sid)
            : null;

        return new ProductLastPurchasePriceDto(
            last?.UnitPrice, last?.Date,
            lastForSupplier?.UnitPrice, lastForSupplier?.Date);
    }
}
