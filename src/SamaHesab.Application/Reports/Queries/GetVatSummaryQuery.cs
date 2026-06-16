using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>
/// فاز ۱۲ (RC) — خلاصهٔ مالیاتِ ارزش‌افزوده در یک بازه: مالیاتِ فروش (خروجی) منهای مالیاتِ خرید (ورودی)
/// = مالیاتِ قابلِ پرداخت. مبنای مشمول = جمعِ ردیف‌ها منهای تخفیف (پیش از مالیات).
/// </summary>
public record VatSummaryDto(
    int SalesCount, decimal SalesBase, decimal OutputVat,
    int PurchaseCount, decimal PurchaseBase, decimal InputVat)
{
    /// <summary>مالیاتِ قابلِ پرداخت (خالص) = خروجی − ورودی (منفی = طلبکار/قابلِ‌استرداد).</summary>
    public decimal NetPayable => OutputVat - InputVat;
}

public record GetVatSummaryQuery(string FromDate, string ToDate) : IRequest<VatSummaryDto>;

public class GetVatSummaryQueryHandler : IRequestHandler<GetVatSummaryQuery, VatSummaryDto>
{
    private readonly ICurrentUserService _user;
    private readonly IRepository<SalesInvoice> _sales;
    private readonly IRepository<PurchaseInvoice> _purchases;

    public GetVatSummaryQueryHandler(ICurrentUserService user,
        IRepository<SalesInvoice> sales, IRepository<PurchaseInvoice> purchases)
    { _user = user; _sales = sales; _purchases = purchases; }

    public async Task<VatSummaryDto> Handle(GetVatSummaryQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        // تاریخِ شمسی yyyy/MM/dd → مقایسهٔ لغوی = مقایسهٔ زمانی.
        var from = req.FromDate; var to = req.ToDate;

        var sales = await _sales.FindAsync(
            i => i.CompanyId == companyId && i.Status == InvoiceStatus.Posted
                 && string.Compare(i.InvoiceDate, from) >= 0 && string.Compare(i.InvoiceDate, to) <= 0, ct);
        var purchases = await _purchases.FindAsync(
            i => i.CompanyId == companyId && i.StatusCode == "قطعی"
                 && string.Compare(i.InvoiceDate, from) >= 0 && string.Compare(i.InvoiceDate, to) <= 0, ct);

        return new VatSummaryDto(
            SalesCount: sales.Count,
            SalesBase: sales.Sum(i => i.SubTotal - i.TotalDiscount),
            OutputVat: sales.Sum(i => i.TotalTax),
            PurchaseCount: purchases.Count,
            PurchaseBase: purchases.Sum(i => i.SubTotal - i.TotalDiscount),
            InputVat: purchases.Sum(i => i.TotalTax));
    }
}
