using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// ATT-C1-4 — گزارشِ کارکردِ ماهانه: برای هر کارمندِ فعال، تجمیعِ ترددِ ماه
/// (حاضر/غایب/مرخصی + اضافه‌کاری/شب‌کاری/جمعه‌کاری + تأخیر/تعجیل) از روی موتورِ ATT-C2-1.
/// </summary>
public record GetAttendanceReportQuery(string Year, byte Month) : IRequest<List<AttendanceReportRow>>;

public record AttendanceReportRow(
    int EmployeeId, string EmployeeName,
    int PresentDays, int AbsentDays, int LeaveDays,
    decimal OvertimeHours, decimal NightHours, decimal HolidayHours,
    int TardyMinutes, int EarlyLeaveMinutes);

public class GetAttendanceReportQueryHandler : IRequestHandler<GetAttendanceReportQuery, List<AttendanceReportRow>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<AttendanceRecord> _records;
    private readonly IRepository<Holiday> _holidays;
    private readonly ICurrentUserService _user;

    public GetAttendanceReportQueryHandler(IRepository<Employee> employees, IRepository<AttendanceRecord> records,
        IRepository<Holiday> holidays, ICurrentUserService user)
    { _employees = employees; _records = records; _holidays = holidays; _user = user; }

    public async Task<List<AttendanceReportRow>> Handle(GetAttendanceReportQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var emps = await _employees.FindAsync(e => e.CompanyId == companyId && e.IsActive, ct);
        if (emps.Count == 0) return new();

        var empIds = emps.Select(e => e.Id).ToHashSet();
        var prefix = $"{req.Year}/{req.Month:D2}/";
        // فیلترِ کارمندِ شرکت در خودِ کوئری (AttendanceRecord بدونِ CompanyId است).
        var recsByEmp = (await _records.FindAsync(
                a => a.WorkDate != null && a.WorkDate.StartsWith(prefix) && empIds.Contains(a.EmployeeId), ct))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var holidaySet = (await _holidays.FindAsync(h => h.CompanyId == companyId, ct))
            .Select(h => h.Date).ToHashSet();

        return emps
            .OrderBy(e => e.LastName)
            .Select(e =>
            {
                var recs = recsByEmp.TryGetValue(e.Id, out var r) ? r : new List<AttendanceRecord>();
                var s = MonthlyAttendanceBuilder.Aggregate(recs, holidaySet);
                return new AttendanceReportRow(e.Id, e.FullName,
                    s.PresentDays, s.AbsentDays, s.LeaveDays,
                    s.OvertimeHours, s.NightHours, s.HolidayHours,
                    s.TotalTardyMinutes, s.TotalEarlyLeaveMinutes);
            })
            .ToList();
    }
}
