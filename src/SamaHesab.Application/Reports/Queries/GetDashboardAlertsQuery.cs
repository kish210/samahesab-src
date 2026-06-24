using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>
/// هشدارهای قابل‌اقدامِ داشبورد. سنجه‌های چک (سررسیدگذشته + ۷روزِ آینده) از تقویمِ سررسیدِ چک
/// و ودیعهٔ کمِ تأمین‌کنندگانِ گردشگری از کوئریِ مربوط تغذیه می‌شود؛ بقیهٔ سنجه‌ها
/// (دریافتنیِ معوق/کسری/ضمانت) نقاطِ توسعه‌اند و فعلاً صفر.
/// </summary>
public record GetDashboardAlertsQuery(string Today) : IRequest<List<ActionableAlert>>;

public class GetDashboardAlertsQueryHandler : IRequestHandler<GetDashboardAlertsQuery, List<ActionableAlert>>
{
    private readonly IChequeRepository _cheques;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ISupplierDepositAlertProvider? _deposits;   // اختیاری — فقط اگر ماژولِ Tourism نصب باشد

    public GetDashboardAlertsQueryHandler(IChequeRepository cheques, IMediator mediator,
        ICurrentUserService currentUser, ISupplierDepositAlertProvider? deposits = null)
    {
        _cheques = cheques;
        _mediator = mediator;
        _currentUser = currentUser;
        _deposits = deposits;
    }

    public async Task<List<ActionableAlert>> Handle(GetDashboardAlertsQuery request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var inProcess = await _cheques.GetByStatusAsync(companyId, ChequeStatus.InProcess, ct);

        var cal = ChequeDueCalendar.Build(
            inProcess.Select(c => new ChequeDueInput(c.DueDate, c.Amount, c.ChequeType == ChequeType.Received)),
            request.Today);

        var overdue = cal.Buckets.Single(b => b.Key == "overdue");
        var week = cal.Buckets.Single(b => b.Key == "week");

        // ودیعهٔ کمِ تأمین‌کنندگانِ گردشگری (از قلابِ اختیاریِ ماژولِ Tourism؛ نبودِ ماژول → ۰).
        var lowDepositCount = _deposits is null ? 0 : await _deposits.LowDepositCountAsync(ct);

        // دریافتنیِ معوقِ مشتریان از ماندهٔ سنی‌شده (معوق = کل − جاری، یعنی بالای ۳۰ روز).
        var aging = await _mediator.Send(new GetAgedBalanceQuery(Payable: false, AsOfDate: request.Today), ct);
        var (overdueRecvCount, overdueRecvAmount) =
            DashboardAlerts.OverdueFromAging(aging.Select(r => (r.Current, r.Total)));

        // کالاهای زیرِ نقطهٔ سفارش/حداقلِ موجودی (پیشنهادهای ری‌اوردر).
        var reorder = await _mediator.Send(new Automation.Queries.GetReorderSuggestionsQuery(), ct);

        var input = new DashboardAlertsInput(
            OverdueChequeCount: overdue.TotalCount, OverdueChequeAmount: overdue.PaidAmount + overdue.ReceivedAmount,
            DueSoonChequeCount: week.TotalCount, DueSoonChequeAmount: week.PaidAmount + week.ReceivedAmount,
            OverdueReceivableCount: overdueRecvCount, OverdueReceivableAmount: overdueRecvAmount,
            LowStockCount: reorder.Count,
            SupplierDepositLowCount: lowDepositCount);

        return DashboardAlerts.Build(input);
    }
}
