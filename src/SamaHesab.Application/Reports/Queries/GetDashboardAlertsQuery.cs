using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Tourism.Commands;
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

    public GetDashboardAlertsQueryHandler(IChequeRepository cheques, IMediator mediator, ICurrentUserService currentUser)
    {
        _cheques = cheques;
        _mediator = mediator;
        _currentUser = currentUser;
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

        // ودیعهٔ کمِ تأمین‌کنندگانِ گردشگری (در صورتِ نبودِ داده، فهرستِ خالی → هشداری ساخته نمی‌شود).
        var lowDeposits = await _mediator.Send(new GetSupplierDepositBalancesQuery(OnlyLow: true), ct);

        var input = new DashboardAlertsInput(
            OverdueChequeCount: overdue.TotalCount, OverdueChequeAmount: overdue.PaidAmount + overdue.ReceivedAmount,
            DueSoonChequeCount: week.TotalCount, DueSoonChequeAmount: week.PaidAmount + week.ReceivedAmount,
            SupplierDepositLowCount: lowDeposits.Count);

        return DashboardAlerts.Build(input);
    }
}
