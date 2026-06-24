namespace SamaHesab.Application.Common.Interfaces;

/// <summary>تجمیعِ ماهانهٔ حضورِ یک کارمند برای حقوق — DTOِ هسته (مستقل از موجودیتِ ماژولِ حضور).</summary>
public sealed record MonthlyAttendanceAggregate(
    decimal OvertimeHours, decimal NightHours, decimal HolidayHours,
    int AbsentDays, int UnpaidLeaveDays);

/// <summary>
/// قراردادِ تأمینِ تجمیعِ حضور برای حقوق — هسته/ماژولِ حقوق فقط این اینترفیس را می‌شناسد، نه ماژولِ حضور.
/// ماژولِ Attendance آن را پیاده می‌کند. اگر ماژولِ حضور نصب/فعال نباشد، هیچ پیاده‌سازی‌ای ثبت نمی‌شود
/// ⇒ حقوق بدونِ کسرِ غیبت/اضافه‌کاریِ خودکار محاسبه می‌شود (هسته/حقوق سالم می‌ماند).
/// </summary>
public interface IAttendanceAggregateProvider
{
    Task<IReadOnlyDictionary<int, MonthlyAttendanceAggregate>> GetMonthlyAsync(
        int companyId, IReadOnlyCollection<int> employeeIds, string year, byte month, CancellationToken ct = default);
}
