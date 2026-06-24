using System.Globalization;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// ATTP-C1-4 — کاردکسِ مرخصیِ یک کارمند در یک سال: فهرستِ درخواست‌ها + مصرفِ تجمعی + ماندهٔ استحقاقی
/// (موتورِ LeaveBalanceCalculator). تأخیر/اضافه‌کاری/غیبت در GetAttendanceReportQuery پوشش دارد.
/// </summary>
public record GetLeaveKardexQuery(int EmployeeId, string Year, decimal CarryOverDays = 0)
    : IRequest<LeaveKardexDto>;

public record LeaveKardexRow(string StartDate, string EndDate, string LeaveType, string Status,
    decimal Days, decimal Hours, decimal RunningUsedDays);

public record LeaveKardexDto(int EmployeeId, string EmployeeName, string Year,
    decimal EntitlementDays, decimal UsedDays, decimal RemainingDays, IReadOnlyList<LeaveKardexRow> Rows);

public class GetLeaveKardexQueryHandler : IRequestHandler<GetLeaveKardexQuery, LeaveKardexDto>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<LeaveRequest> _leaves;
    private readonly ICurrentUserService _user;

    public GetLeaveKardexQueryHandler(IRepository<Employee> employees, IRepository<LeaveRequest> leaves, ICurrentUserService user)
    { _employees = employees; _leaves = leaves; _user = user; }

    public async Task<LeaveKardexDto> Handle(GetLeaveKardexQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var emp = await _employees.FindSingleAsync(e => e.Id == req.EmployeeId && e.CompanyId == companyId, ct);
        var name = emp?.FullName ?? "—";

        var prefix = req.Year + "/";
        var all = (await _leaves.FindAsync(l => l.CompanyId == companyId && l.EmployeeId == req.EmployeeId, ct))
            .Where(l => l.StartDate != null && l.StartDate.StartsWith(prefix))
            .OrderBy(l => l.StartDate).ToList();

        var rules = new LeaveRules();
        decimal runningAnnual = 0;
        var rows = new List<LeaveKardexRow>();
        foreach (var l in all)
        {
            // فقط مرخصیِ استحقاقیِ تأییدشده در ماندهٔ سالانه لحاظ می‌شود.
            if (l.LeaveType == LeaveRequest.TypeAnnual && l.Status == LeaveRequest.StatusApproved)
                runningAnnual += l.Days + (rules.WorkHoursPerDay > 0 ? l.Hours / rules.WorkHoursPerDay : 0);
            rows.Add(new LeaveKardexRow(l.StartDate, l.EndDate, l.LeaveType, l.Status, l.Days, l.Hours,
                Round(runningAnnual)));
        }

        var used = new LeaveUsage(
            all.Where(l => l.LeaveType == LeaveRequest.TypeAnnual && l.Status == LeaveRequest.StatusApproved).Sum(l => l.Days),
            all.Where(l => l.LeaveType == LeaveRequest.TypeAnnual && l.Status == LeaveRequest.StatusApproved).Sum(l => l.Hours));
        var balance = LeaveBalanceCalculator.Compute(12, used, req.CarryOverDays, rules);

        return new LeaveKardexDto(req.EmployeeId, name, req.Year,
            balance.EntitlementDays, balance.UsedDays, balance.RemainingDays, rows);
    }

    private static decimal Round(decimal v) => System.Math.Round(v, 2, System.MidpointRounding.AwayFromZero);
}
