using System.Globalization;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// ATT-C1-3 — تجمیعِ ماهانهٔ ترددِ یک کارمند از رکوردهای DB و عبور از موتورِ `AttendanceCalculator`
/// (ATT-C2-1). خروجی (`MonthlyAttendance`) مستقیماً ورودیِ موتورِ حقوق (`FullPayrollInput`) را پر می‌کند.
/// </summary>
public record GetMonthlyAttendanceQuery(int EmployeeId, string Year, byte Month)
    : IRequest<MonthlyAttendanceDto>;

public record MonthlyAttendanceDto(
    int EmployeeId, string EmployeeName, string Year, byte Month, MonthlyAttendance Summary);

public class GetMonthlyAttendanceQueryHandler : IRequestHandler<GetMonthlyAttendanceQuery, MonthlyAttendanceDto>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<AttendanceRecord> _records;
    private readonly IRepository<Holiday> _holidays;
    private readonly ICurrentUserService _user;

    public GetMonthlyAttendanceQueryHandler(IRepository<Employee> employees, IRepository<AttendanceRecord> records,
        IRepository<Holiday> holidays, ICurrentUserService user)
    { _employees = employees; _records = records; _holidays = holidays; _user = user; }

    public async Task<MonthlyAttendanceDto> Handle(GetMonthlyAttendanceQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var emp = await _employees.FindSingleAsync(e => e.Id == req.EmployeeId && e.CompanyId == companyId, ct);
        var name = emp?.FullName ?? "—";

        var prefix = $"{req.Year}/{req.Month:D2}/";
        var recs = (await _records.FindAsync(
            a => a.EmployeeId == req.EmployeeId && a.WorkDate != null && a.WorkDate.StartsWith(prefix), ct)).ToList();
        var holidaySet = (await _holidays.FindAsync(h => h.CompanyId == companyId, ct))
            .Select(h => h.Date).ToHashSet();

        var summary = MonthlyAttendanceBuilder.Aggregate(recs, holidaySet);
        return new MonthlyAttendanceDto(req.EmployeeId, name, req.Year, req.Month, summary);
    }
}

/// <summary>
/// پلِ DB→موتور: رکوردهای ترددِ یک ماه را به `DayAttendance` نگاشت می‌کند (تشخیصِ جمعه/تعطیل با تقویم)
/// و خروجیِ ماهانه را می‌سازد. توسطِ کوئریِ تجمیع و فرمانِ حقوق (PAY-C1-3) به اشتراک گذاشته می‌شود.
/// </summary>
public static class MonthlyAttendanceBuilder
{
    public static MonthlyAttendance Aggregate(IEnumerable<AttendanceRecord> records,
        IReadOnlySet<string> holidayDates, AttendanceRules? rules = null)
    {
        var days = records.Select(r => new DayAttendance(
            CheckIn: r.CheckIn,
            CheckOut: r.CheckOut,
            IsHoliday: IsHoliday(r.WorkDate, holidayDates),
            Status: r.Status,
            LeaveType: r.LeaveType));
        return AttendanceCalculator.AggregateMonth(days, rules ?? AttendanceRules.Default);
    }

    /// <summary>روزِ تعطیل = جمعه (از تقویمِ شمسی) یا در فهرستِ تعطیلاتِ ثبت‌شده.</summary>
    public static bool IsHoliday(string? shamsiDate, IReadOnlySet<string> holidayDates)
    {
        if (string.IsNullOrWhiteSpace(shamsiDate)) return false;
        if (holidayDates.Contains(shamsiDate)) return true;
        return IsFriday(shamsiDate);
    }

    public static bool IsFriday(string shamsiDate)
    {
        var p = shamsiDate.Replace('-', '/').Split('/');
        if (p.Length < 3) return false;
        if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ||
            !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d))
            return false;
        if (y < 1 || m < 1 || m > 12 || d < 1 || d > 31) return false;
        try
        {
            var pc = new PersianCalendar();
            return pc.ToDateTime(y, m, d, 0, 0, 0, 0).DayOfWeek == DayOfWeek.Friday;
        }
        catch { return false; }
    }
}
