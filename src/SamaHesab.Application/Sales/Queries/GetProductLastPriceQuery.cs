using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Queries;

/// <summary>
/// UX-SALES-1 — آخرین قیمتِ فروشِ یک کالا (برای هینتِ تاریخچهٔ قیمت در فاکتور فروش).
/// «کلی» = آخرین فروشِ این کالا به هر مشتری؛ «این مشتری» = آخرین فروش به مشتریِ جاری.
/// </summary>
public record GetProductLastPriceQuery(int ProductId, int? CustomerId) : IRequest<ProductLastPriceDto>;

public record ProductLastPriceDto(
    decimal? LastPrice, string? LastDate,
    decimal? LastPriceForCustomer, string? LastDateForCustomer);

public class GetProductLastPriceQueryHandler
    : IRequestHandler<GetProductLastPriceQuery, ProductLastPriceDto>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceItem> _items;
    private readonly ICurrentUserService _currentUser;

    public GetProductLastPriceQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<SalesInvoiceItem> items, ICurrentUserService currentUser)
    { _invoices = invoices; _items = items; _currentUser = currentUser; }

    public async Task<ProductLastPriceDto> Handle(GetProductLastPriceQuery req, CancellationToken ct)
    {
        if (req.ProductId <= 0) return new ProductLastPriceDto(null, null, null, null);
        var companyId = _currentUser.CompanyId ?? 1;

        // فاکتورهای قطعی/تأییدِ فروش (نه پیش‌نویس/لغو/مرجوعی) — نگاشتِ Id→تاریخ و Id→مشتری.
        var sales = await _invoices.FindAsync(
            i => i.CompanyId == companyId
                 && i.InvoiceType == InvoiceType.Sale
                 && i.Status != InvoiceStatus.Draft
                 && i.Status != InvoiceStatus.Cancelled, ct);
        var dateOf = sales.ToDictionary(i => i.Id, i => i.InvoiceDate);
        var custOf = sales.ToDictionary(i => i.Id, i => i.CustomerId);

        // ردیف‌های همین کالا که فاکتورشان فروشِ معتبر است.
        var lines = (await _items.FindAsync(it => it.ProductId == req.ProductId, ct))
            .Where(it => dateOf.ContainsKey(it.InvoiceId))
            .Select(it => new { it.UnitPrice, Date = dateOf[it.InvoiceId], Customer = custOf[it.InvoiceId] })
            .OrderByDescending(x => x.Date, StringComparer.Ordinal)
            .ToList();

        var last = lines.FirstOrDefault();
        var lastForCustomer = req.CustomerId is int cid && cid > 0
            ? lines.FirstOrDefault(x => x.Customer == cid)
            : null;

        return new ProductLastPriceDto(
            last?.UnitPrice, last?.Date,
            lastForCustomer?.UnitPrice, lastForCustomer?.Date);
    }
}
