using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>
/// هشدارهای قابل‌اقدامِ داشبورد. سنجه‌های چک (سررسیدگذشته + ۷روزِ آینده) از تقویمِ سررسیدِ چک
/// تغذیه می‌شود؛ بقیهٔ سنجه‌ها (دریافتنیِ معوق/کسری/ضمانت) نقاطِ توسعه‌اند و فعلاً صفر.
/// </summary>
public record GetDashboardAlertsQuery(string Today) : IRequest<List<ActionableAlert>>;

public class GetDashboardAlertsQueryHandler : IRequestHandler<GetDashboardAlertsQuery, List<ActionableAlert>>
{
    private readonly IChequeRepository _cheques;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardAlertsQueryHandler(IChequeRepository cheques, ICurrentUserService currentUser)
    {
        _cheques = cheques;
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

        var input = new DashboardAlertsInput(
            OverdueChequeCount: overdue.TotalCount, OverdueChequeAmount: overdue.PaidAmount + overdue.ReceivedAmount,
            DueSoonChequeCount: week.TotalCount, DueSoonChequeAmount: week.PaidAmount + week.ReceivedAmount);

        return DashboardAlerts.Build(input);
    }
}
