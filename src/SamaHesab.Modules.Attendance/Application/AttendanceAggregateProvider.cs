using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;            // MonthlyAttendanceBuilder/MonthlyAttendance (namespace حفظ‌شده)
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Attendance.Application;

/// <summary>
/// پیاده‌سازیِ ماژولِ حضور از قراردادِ تجمیعِ حقوق: رکوردهای ماهِ هر کارمند را به کارکرد/اضافه‌کاری/
/// غیبت تبدیل می‌کند (با تقویمِ تعطیلات). حقوق این را از طریقِ اینترفیسِ هسته می‌گیرد، بدونِ
/// وابستگیِ مستقیم به موجودیتِ تردد.
/// </summary>
public sealed class AttendanceAggregateProvider : IAttendanceAggregateProvider
{
    private readonly IRepository<AttendanceRecord> _records;
    private readonly IRepository<Holiday> _holidays;

    public AttendanceAggregateProvider(IRepository<AttendanceRecord> records, IRepository<Holiday> holidays)
    { _records = records; _holidays = holidays; }

    public async Task<IReadOnlyDictionary<int, MonthlyAttendanceAggregate>> GetMonthlyAsync(
        int companyId, IReadOnlyCollection<int> employeeIds, string year, byte month, CancellationToken ct = default)
    {
        var prefix = $"{year}/{month:D2}/";
        var holidaySet = (await _holidays.FindAsync(h => h.CompanyId == companyId, ct))
            .Select(h => h.Date).ToHashSet();
        var monthRecs = (await _records.FindAsync(
                a => a.WorkDate != null && a.WorkDate.StartsWith(prefix) && employeeIds.Contains(a.EmployeeId), ct))
            .GroupBy(a => a.EmployeeId);

        var result = new Dictionary<int, MonthlyAttendanceAggregate>();
        foreach (var g in monthRecs)
        {
            var m = MonthlyAttendanceBuilder.Aggregate(g, holidaySet);
            result[g.Key] = new MonthlyAttendanceAggregate(
                m.OvertimeHours, m.NightHours, m.HolidayHours, m.AbsentDays, m.UnpaidLeaveDays);
        }
        return result;
    }
}
