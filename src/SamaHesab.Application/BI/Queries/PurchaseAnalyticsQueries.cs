using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

// ملاحظه: GetTopSuppliersQuery از قبل در PurchaseAndBranchQueries.cs تعریف شده؛ اینجا فقط روندِ خرید افزوده می‌شود.

public record PurchaseTrendDto(string Period, decimal Total, int Count);

// ── Purchase Trend (monthly) ─────────────────────────────────────────────────
public record GetPurchaseTrendQuery(string FromDate, string ToDate) : IRequest<List<PurchaseTrendDto>>;

public class GetPurchaseTrendQueryHandler : IRequestHandler<GetPurchaseTrendQuery, List<PurchaseTrendDto>>
{
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly ICurrentUserService _currentUser;

    public GetPurchaseTrendQueryHandler(IRepository<PurchaseInvoice> invoices, ICurrentUserService currentUser)
    { _invoices = invoices; _currentUser = currentUser; }

    public async Task<List<PurchaseTrendDto>> Handle(GetPurchaseTrendQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var all = await _invoices.FindAsync(
            i => i.CompanyId == companyId && i.InvoiceType == "خرید" && i.StatusCode == "قطعی", ct);
        var inRange = all.Where(i => string.CompareOrdinal(i.InvoiceDate, req.FromDate) >= 0
                                  && string.CompareOrdinal(i.InvoiceDate, req.ToDate) <= 0);
        return SalesAnalytics
            .MonthlyTrend(inRange.Select(i => new SalesRecord(i.InvoiceDate, i.SupplierId, i.GrandTotal)))
            .Select(p => new PurchaseTrendDto(p.Period, p.Total, p.Count))
            .ToList();
    }
}
