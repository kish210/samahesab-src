using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Automation.Queries;

/// <summary>
/// مونتاژِ پیامک‌های یادآور (P2): دریافتنیِ معوقِ مشتری (فاکتورِ بازِ سررسیدگذشته) + چکِ دریافتیِ
/// نزدیکِ سررسید — همراهِ موبایلِ طرف‌حساب. خروجی آمادهٔ نمایش/ارسال است؛ ارسال اقدامِ صریحِ کاربر است.
/// </summary>
public record GetOverdueRemindersQuery(string Today, string CompanyName, int ChequeDueWithinDays = 7)
    : IRequest<IReadOnlyList<Reminder>>;

public class GetOverdueRemindersQueryHandler : IRequestHandler<GetOverdueRemindersQuery, IReadOnlyList<Reminder>>
{
    private readonly IRepository<SalesInvoice> _sales;
    private readonly IRepository<Party> _parties;
    private readonly IChequeRepository _cheques;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _user;

    public GetOverdueRemindersQueryHandler(IRepository<SalesInvoice> sales, IRepository<Party> parties,
        IChequeRepository cheques, IPersianCalendarService calendar, ICurrentUserService user)
    { _sales = sales; _parties = parties; _cheques = cheques; _calendar = calendar; _user = user; }

    public async Task<IReadOnlyList<Reminder>> Handle(GetOverdueRemindersQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var parties = (await _parties.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id);

        var inputs = new List<ReminderInput>();

        // ── دریافتنیِ معوق: فاکتورِ فروشِ قطعیِ بازِ سررسیدگذشته، per-مشتری ──
        var openInvoices = await _sales.FindAsync(
            i => i.CompanyId == companyId && i.Status == InvoiceStatus.Posted && i.RemainAmount > 0.01m, ct);
        var overdueByCustomer = openInvoices
            .Where(i => string.CompareOrdinal(i.DueDate ?? i.InvoiceDate, req.Today) < 0)   // سررسیدگذشته
            .GroupBy(i => i.CustomerId)
            .Select(g => (CustomerId: g.Key, Amount: g.Sum(i => i.RemainAmount)));
        foreach (var (customerId, amount) in overdueByCustomer)
        {
            var p = parties.GetValueOrDefault(customerId);
            inputs.Add(new ReminderInput(p?.FullName ?? $"#{customerId}", p?.Mobile, amount, ReminderKind.OverdueDebt));
        }

        // ── چکِ دریافتیِ نزدیکِ سررسید (تا N روز، هنوز در جریان) ──
        string bound;
        try { bound = _calendar.ToPersianDate(_calendar.ToGregorianDate(req.Today).AddDays(req.ChequeDueWithinDays)); }
        catch { bound = req.Today; }
        var inProcess = await _cheques.GetByStatusAsync(companyId, ChequeStatus.InProcess, ct);
        foreach (var c in inProcess.Where(c => c.ChequeType == ChequeType.Received
            && string.CompareOrdinal(c.DueDate, bound) <= 0
            && c.PartyId is not null))
        {
            var p = parties.GetValueOrDefault(c.PartyId!.Value);
            inputs.Add(new ReminderInput(p?.FullName ?? $"#{c.PartyId}", p?.Mobile, c.Amount, ReminderKind.ChequeDueSoon, c.DueDate));
        }

        return OverdueReminderBuilder.Build(inputs, req.CompanyName);
    }
}
