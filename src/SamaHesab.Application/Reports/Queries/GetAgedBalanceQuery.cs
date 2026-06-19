using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>یک ردیفِ ماندهٔ سنی‌شده برای یک طرف‌حساب (مشتری/تأمین‌کننده).</summary>
public record AgedRow(string PartyName, decimal Current, decimal D31_60, decimal D61_90, decimal Over90, decimal Total);

/// <summary>
/// فاز ۱۲ (RC) — ماندهٔ سنی‌شدهٔ دریافتنی/پرداختنی تا تاریخِ مشخص.
/// <paramref name="Payable"/>=false ⇒ دریافتنی (فاکتورِ فروشِ بازِ مشتری) · true ⇒ پرداختنی (خریدِ بازِ تأمین‌کننده).
/// سن بر اساسِ سررسید (در نبودِ سررسید، تاریخِ سند) محاسبه می‌شود.
/// </summary>
public record GetAgedBalanceQuery(bool Payable, string AsOfDate) : IRequest<List<AgedRow>>;

public class GetAgedBalanceQueryHandler : IRequestHandler<GetAgedBalanceQuery, List<AgedRow>>
{
    private readonly ICurrentUserService _user;
    private readonly IPersianCalendarService _calendar;
    private readonly IRepository<SalesInvoice> _sales;
    private readonly IRepository<PurchaseInvoice> _purchases;
    private readonly IRepository<Party> _customers;
    private readonly IRepository<Party> _suppliers;

    public GetAgedBalanceQueryHandler(ICurrentUserService user, IPersianCalendarService calendar,
        IRepository<SalesInvoice> sales, IRepository<PurchaseInvoice> purchases,
        IRepository<Party> customers, IRepository<Party> suppliers)
    {
        _user = user; _calendar = calendar; _sales = sales; _purchases = purchases;
        _customers = customers; _suppliers = suppliers;
    }

    public async Task<List<AgedRow>> Handle(GetAgedBalanceQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;

        DateTime asOf;
        try { asOf = _calendar.ToGregorianDate(req.AsOfDate); } catch { asOf = DateTime.Today; }

        int Age(string? due, string invoiceDate)
        {
            var d = string.IsNullOrWhiteSpace(due) ? invoiceDate : due!;
            try { return (int)(asOf - _calendar.ToGregorianDate(d)).TotalDays; } catch { return 0; }
        }

        var byParty = new Dictionary<int, AgingBuckets>();
        var names = new Dictionary<int, string>();

        if (!req.Payable)
        {
            var open = await _sales.FindAsync(
                i => i.CompanyId == companyId && i.Status == InvoiceStatus.Posted && i.RemainAmount > 0.01m, ct);
            foreach (var i in open)
            {
                if (!byParty.TryGetValue(i.CustomerId, out var b)) byParty[i.CustomerId] = b = new AgingBuckets();
                b.Add(i.RemainAmount, Age(i.DueDate, i.InvoiceDate));
            }
            foreach (var c in await _customers.FindAsync(c => c.CompanyId == companyId && c.IsCustomer, ct))
                names[c.Id] = c.FullName;
        }
        else
        {
            var open = await _purchases.FindAsync(
                i => i.CompanyId == companyId && i.StatusCode == "قطعی" && i.RemainAmount > 0.01m, ct);
            foreach (var i in open)
            {
                if (!byParty.TryGetValue(i.SupplierId, out var b)) byParty[i.SupplierId] = b = new AgingBuckets();
                b.Add(i.RemainAmount, Age(i.DueDate, i.InvoiceDate));
            }
            foreach (var s in await _suppliers.FindAsync(s => s.CompanyId == companyId && s.IsSupplier, ct))
                names[s.Id] = s.FullName;
        }

        return byParty
            .Select(kv => new AgedRow(
                names.TryGetValue(kv.Key, out var n) ? n : $"#{kv.Key}",
                kv.Value.Current, kv.Value.D31_60, kv.Value.D61_90, kv.Value.Over90, kv.Value.Total))
            .OrderByDescending(r => r.Total)
            .ToList();
    }
}
