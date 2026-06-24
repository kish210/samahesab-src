namespace SamaHesab.Application.HRM;

/// <summary>قواعدِ شیفت و تجمیعِ تردد — قابلِ‌پیکربندی (هر کارگاه/شیفت).</summary>
public record AttendanceRules(
    decimal StandardDailyHours = 7.33m,  // ساعتِ کارِ مؤظفِ روزانه (۴۴ ساعت ÷ ۶ روز ≈ ۷:۲۰)
    TimeOnly ShiftStart = default,       // شروعِ شیفت (پیش‌فرض ۰۸:۰۰ — در سازنده ست می‌شود)
    TimeOnly ShiftEnd = default,         // پایانِ شیفت
    int GraceMinutes = 0,                // ارفاقِ تأخیر/تعجیل (دقیقه)
    TimeOnly NightStart = default,       // شروعِ بازهٔ شب‌کاری (۲۲:۰۰)
    TimeOnly NightEnd = default)         // پایانِ بازهٔ شب‌کاری (۰۶:۰۰)
{
    /// <summary>پیش‌فرض‌های قانونِ کار: شیفتِ ۸ تا ۱۶، شب‌کاری ۲۲ تا ۶.</summary>
    public static AttendanceRules Default => new(
        StandardDailyHours: 7.33m,
        ShiftStart: new TimeOnly(8, 0), ShiftEnd: new TimeOnly(16, 0),
        GraceMinutes: 0,
        NightStart: new TimeOnly(22, 0), NightEnd: new TimeOnly(6, 0));
}

/// <summary>ترددِ یک روز برای یک کارمند (ورودیِ خالص).</summary>
public record DayAttendance(
    TimeOnly? CheckIn, TimeOnly? CheckOut,
    bool IsHoliday = false,          // جمعه/تعطیلِ رسمی (کارکردِ این روز جمعه‌کاری است)
    string Status = "حاضر",          // حاضر/غایب/مرخصی/مأموریت/تعطیل
    string? LeaveType = null);       // نوعِ مرخصی (استحقاقی/استعلاجی/بدون‌حقوق/ساعتی)

/// <summary>نتیجهٔ محاسبهٔ یک روز.</summary>
public record DayResult(
    decimal WorkedHours,    // کلِ ساعتِ حضور
    decimal NormalHours,    // ساعتِ عادی (تا سقفِ مؤظف، در روزِ غیرتعطیل)
    decimal OvertimeHours,  // اضافه‌کاریِ روزِ عادی (مازادِ مؤظف)
    decimal NightHours,     // ساعتِ واقع در بازهٔ ۲۲–۶
    decimal HolidayHours,   // ساعتِ کارکرد در روزِ تعطیل/جمعه
    int TardyMinutes,       // تأخیرِ ورود (پس از ارفاق)
    int EarlyLeaveMinutes); // تعجیلِ خروج (پس از ارفاق)

/// <summary>جمعِ ماهانهٔ تردد — مستقیماً فیلدهای حقوق را پر می‌کند.</summary>
public record MonthlyAttendance(
    int PresentDays, int AbsentDays, int LeaveDays, int PaidLeaveDays, int UnpaidLeaveDays,
    int HolidayWorkDays,
    decimal WorkedHours, decimal OvertimeHours, decimal NightHours, decimal HolidayHours,
    int TotalTardyMinutes, int TotalEarlyLeaveMinutes);

/// <summary>
/// موتورِ خالصِ محاسبهٔ تردد — مستقل و تست‌پذیر، بدونِ وابستگی به DB.
/// <see cref="ComputeDay"/> یک روز و <see cref="AggregateMonth"/> یک ماه را محاسبه می‌کند؛
/// خروجیِ ماهانه ورودیِ مستقیمِ موتورِ حقوق (<see cref="PayrollCalculator.ComputeFull"/>) است.
/// </summary>
public static class AttendanceCalculator
{
    public static DayResult ComputeDay(DayAttendance day, AttendanceRules? rules = null)
    {
        var r = rules ?? AttendanceRules.Default;

        // روزِ بدونِ حضور (غیبت/مرخصی/تعطیلِ کارنکرده) → همه صفر.
        if (day.CheckIn is not { } ci || day.CheckOut is not { } co || co <= ci)
            return new DayResult(0, 0, 0, 0, 0, 0, 0);

        var worked = Hours(ci, co);
        var night = OverlapHours(ci, co, r.NightStart, r.NightEnd);

        decimal normal = 0, overtime = 0, holiday = 0;
        if (day.IsHoliday)
        {
            holiday = worked;                       // کلِ کارکردِ روزِ تعطیل = جمعه‌کاری
        }
        else
        {
            normal = Math.Min(worked, r.StandardDailyHours);
            overtime = Math.Max(0, worked - r.StandardDailyHours);
        }

        int tardy = 0, early = 0;
        if (!day.IsHoliday)
        {
            tardy = LateMinutes(r.ShiftStart, ci, r.GraceMinutes);
            early = LateMinutes(co, r.ShiftEnd, r.GraceMinutes);
        }

        return new DayResult(Round(worked), Round(normal), Round(overtime),
            Round(night), Round(holiday), tardy, early);
    }

    public static MonthlyAttendance AggregateMonth(IEnumerable<DayAttendance> days, AttendanceRules? rules = null)
    {
        int present = 0, absent = 0, leave = 0, paidLeave = 0, unpaidLeave = 0, holidayWork = 0;
        decimal worked = 0, ot = 0, night = 0, holiday = 0;
        int tardy = 0, early = 0;

        foreach (var d in days)
        {
            var status = d.Status?.Trim() ?? "";
            if (status == "غایب") { absent++; continue; }
            if (status == "مرخصی")
            {
                leave++;
                if (IsUnpaidLeave(d.LeaveType)) unpaidLeave++; else paidLeave++;
                continue;
            }

            var res = ComputeDay(d, rules);
            if (res.WorkedHours <= 0) continue;     // بدونِ تردد و بدونِ وضعیتِ خاص → نادیده

            present++;
            if (d.IsHoliday) holidayWork++;
            worked += res.WorkedHours;
            ot += res.OvertimeHours;
            night += res.NightHours;
            holiday += res.HolidayHours;
            tardy += res.TardyMinutes;
            early += res.EarlyLeaveMinutes;
        }

        return new MonthlyAttendance(present, absent, leave, paidLeave, unpaidLeave, holidayWork,
            Round(worked), Round(ot), Round(night), Round(holiday), tardy, early);
    }

    // ── helpers ──

    private static bool IsUnpaidLeave(string? type) =>
        !string.IsNullOrWhiteSpace(type) && type.Contains("بدون");

    /// <summary>اختلافِ ساعتِ دو زمان (co > ci فرض شده).</summary>
    private static decimal Hours(TimeOnly from, TimeOnly to) =>
        (decimal)(to.ToTimeSpan() - from.ToTimeSpan()).TotalHours;

    /// <summary>دقیقهٔ تأخیر = چه‌قدر <paramref name="actual"/> از <paramref name="reference"/> دیرتر است (پس از ارفاق).</summary>
    private static int LateMinutes(TimeOnly reference, TimeOnly actual, int grace)
    {
        var diff = (int)Math.Round((actual.ToTimeSpan() - reference.ToTimeSpan()).TotalMinutes);
        return Math.Max(0, diff - grace);
    }

    /// <summary>
    /// ساعتِ همپوشانیِ بازهٔ کارکرد [in,out] با بازهٔ شب [nightStart,nightEnd].
    /// بازهٔ شب از شب تا صبحِ روزِ بعد است (مثلاً ۲۲:۰۰ تا ۰۶:۰۰) → به دو بخش تقسیم می‌شود.
    /// </summary>
    private static decimal OverlapHours(TimeOnly inT, TimeOnly outT, TimeOnly nightStart, TimeOnly nightEnd)
    {
        // فرض: شیفت در یک روزِ تقویمی (in<out). بازهٔ شب = [nightStart..24) ∪ [0..nightEnd).
        decimal total = 0;
        total += Overlap(inT, outT, nightStart, TimeOnly.MaxValue);  // بخشِ عصر/شب تا پایانِ روز
        total += Overlap(inT, outT, TimeOnly.MinValue, nightEnd);    // بخشِ بامداد تا nightEnd
        return total;
    }

    /// <summary>همپوشانیِ ساعتِ دو بازهٔ هم‌روز [a1,a2] و [b1,b2].</summary>
    private static decimal Overlap(TimeOnly a1, TimeOnly a2, TimeOnly b1, TimeOnly b2)
    {
        var lo = a1 > b1 ? a1 : b1;
        var hi = a2 < b2 ? a2 : b2;
        if (hi <= lo) return 0;
        return Hours(lo, hi);
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
