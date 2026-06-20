using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

// ── Sales-Rep Dashboard ──────────────────────────────────────────────────────
// داشبوردِ اختصاصیِ فروشنده: فروشِ امروز/ماهِ همین کاربر + طلبِ نوصول + فاکتورهای معوق.
// فیلتر بر SalesRepId == کاربرِ جاری (اگر کاربر فروشنده نباشد، نتیجه صفر است).
public record GetSalesRepDashboardQuery(string Today, string MonthStart) : IRequest<SalesRepDashboardDto>;

public record SalesRepDashboardDto(
    decimal TodaySales, int TodayCount, decimal TodayAverage,
    decimal MonthSales, int MonthCount,
    int UnpaidCount, decimal UnpaidTotal,
    int OverdueCount, decimal OverdueTotal);

public class GetSalesRepDashboardQueryHandler
    : IRequestHandler<GetSalesRepDashboardQuery, SalesRepDashboardDto>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly ICurrentUserService _currentUser;

    public GetSalesRepDashboardQueryHandler(IRepository<SalesInvoice> invoices, ICurrentUserService currentUser)
    { _invoices = invoices; _currentUser = currentUser; }

    public async Task<SalesRepDashboardDto> Handle(GetSalesRepDashboardQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var repId = _currentUser.UserId ?? 0;

        // فاکتورهای قطعی/تأییدِ فروشِ همین فروشنده (فیلترِ شرکت/نوع/وضعیت در SQL).
        var mine = await _invoices.FindAsync(
            i => i.CompanyId == companyId
                 && i.InvoiceType == InvoiceType.Sale
                 && i.Status != InvoiceStatus.Draft
                 && i.Status != InvoiceStatus.Cancelled
                 && i.SalesRepId == repId, ct);

        var today = mine
            .Where(i => string.CompareOrdinal(i.InvoiceDate, req.Today) == 0)
            .ToList();
        var month = mine
            .Where(i => string.CompareOrdinal(i.InvoiceDate, req.MonthStart) >= 0
                     && string.CompareOrdinal(i.InvoiceDate, req.Today) <= 0)
            .ToList();

        var todaySales = today.Sum(i => i.GrandTotal);
        var todayCount = today.Count;
        var todayAvg = todayCount == 0 ? 0 : Math.Round(todaySales / todayCount, 0);

        // طلبِ نوصول: فاکتورهایی که هنوز مانده دارند.
        var unpaid = mine.Where(i => i.RemainAmount > 0.01m).ToList();
        // معوق: مانده‌دار با سررسیدِ گذشته (DueDate لغوی کوچک‌تر از امروز).
        var overdue = unpaid
            .Where(i => !string.IsNullOrEmpty(i.DueDate)
                     && string.CompareOrdinal(i.DueDate, req.Today) < 0)
            .ToList();

        return new SalesRepDashboardDto(
            todaySales, todayCount, todayAvg,
            month.Sum(i => i.GrandTotal), month.Count,
            unpaid.Count, unpaid.Sum(i => i.RemainAmount),
            overdue.Count, overdue.Sum(i => i.RemainAmount));
    }
}
