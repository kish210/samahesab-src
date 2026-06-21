using SamaHesab.Application.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ATT-C2-1 — موتورِ خالصِ تردد (روز + تجمیعِ ماه).</summary>
public class AttendanceCalculatorTests
{
    private static readonly AttendanceRules Rules = AttendanceRules.Default; // شیفت ۰۸–۱۶، شب ۲۲–۶

    [Fact]
    public void Normal_Day_Splits_Work_And_Overtime()
    {
        // ۰۸:۰۰ تا ۱۸:۰۰ = ۱۰ ساعت ؛ مؤظف ۷.۳۳ → اضافه‌کاری ≈ ۲.۶۷
        var d = AttendanceCalculator.ComputeDay(
            new DayAttendance(new TimeOnly(8, 0), new TimeOnly(18, 0)), Rules);
        Assert.Equal(10m, d.WorkedHours);
        Assert.Equal(7.33m, d.NormalHours);
        Assert.Equal(2.67m, d.OvertimeHours);
        Assert.Equal(0, d.TardyMinutes);
    }

    [Fact]
    public void Holiday_Work_All_Counts_As_HolidayHours()
    {
        var d = AttendanceCalculator.ComputeDay(
            new DayAttendance(new TimeOnly(9, 0), new TimeOnly(14, 0), IsHoliday: true), Rules);
        Assert.Equal(5m, d.WorkedHours);
        Assert.Equal(5m, d.HolidayHours);
        Assert.Equal(0m, d.OvertimeHours);
        Assert.Equal(0, d.TardyMinutes);   // روزِ تعطیل تأخیر ندارد
    }

    [Fact]
    public void Tardy_And_EarlyLeave_Measured_From_Shift()
    {
        // ورود ۰۸:۲۰ (۲۰ دقیقه تأخیر)، خروج ۱۵:۴۵ (۱۵ دقیقه تعجیل)
        var d = AttendanceCalculator.ComputeDay(
            new DayAttendance(new TimeOnly(8, 20), new TimeOnly(15, 45)), Rules);
        Assert.Equal(20, d.TardyMinutes);
        Assert.Equal(15, d.EarlyLeaveMinutes);
    }

    [Fact]
    public void Grace_Reduces_Tardy()
    {
        var rules = Rules with { GraceMinutes = 15 };
        var d = AttendanceCalculator.ComputeDay(
            new DayAttendance(new TimeOnly(8, 20), new TimeOnly(16, 0)), rules);
        Assert.Equal(5, d.TardyMinutes);   // ۲۰ − ۱۵ ارفاق
    }

    [Fact]
    public void Night_Hours_Counted_In_22_To_6_Band()
    {
        // ۱۸:۰۰ تا ۲۴:۰۰ مرزِ روز؛ ۲۰:۰۰ تا ۲۳:۰۰ → فقط ۲۲:۰۰–۲۳:۰۰ شب‌کاری = ۱ ساعت
        var d = AttendanceCalculator.ComputeDay(
            new DayAttendance(new TimeOnly(20, 0), new TimeOnly(23, 0)), Rules);
        Assert.Equal(1m, d.NightHours);
    }

    [Fact]
    public void Early_Morning_Counts_As_Night()
    {
        // ۰۴:۰۰ تا ۰۸:۰۰ → ۰۴–۰۶ شب‌کاری = ۲ ساعت
        var d = AttendanceCalculator.ComputeDay(
            new DayAttendance(new TimeOnly(4, 0), new TimeOnly(8, 0)), Rules);
        Assert.Equal(2m, d.NightHours);
    }

    [Fact]
    public void Absent_Or_Leave_Day_Is_Zero()
    {
        var absent = AttendanceCalculator.ComputeDay(new DayAttendance(null, null, Status: "غایب"), Rules);
        Assert.Equal(0m, absent.WorkedHours);
    }

    [Fact]
    public void AggregateMonth_Sums_All_Buckets()
    {
        var days = new[]
        {
            new DayAttendance(new TimeOnly(8, 0), new TimeOnly(18, 0)),                 // عادی: ۱۰ کار، ۲.۶۷ اضافه
            new DayAttendance(new TimeOnly(8, 0), new TimeOnly(16, 0)),                 // عادی: ۸ کار
            new DayAttendance(new TimeOnly(9, 0), new TimeOnly(14, 0), IsHoliday: true),// جمعه‌کاری ۵
            new DayAttendance(null, null, Status: "غایب"),                              // غیبت
            new DayAttendance(null, null, Status: "مرخصی", LeaveType: "استحقاقی"),       // مرخصیِ باحقوق
            new DayAttendance(null, null, Status: "مرخصی", LeaveType: "بدون‌حقوق"),       // مرخصیِ بی‌حقوق
        };
        var m = AttendanceCalculator.AggregateMonth(days, Rules);

        Assert.Equal(3, m.PresentDays);          // دو روزِ عادی + یک روزِ تعطیلِ کارکرده
        Assert.Equal(1, m.AbsentDays);
        Assert.Equal(2, m.LeaveDays);
        Assert.Equal(1, m.PaidLeaveDays);
        Assert.Equal(1, m.UnpaidLeaveDays);
        Assert.Equal(1, m.HolidayWorkDays);
        Assert.Equal(3.34m, m.OvertimeHours);   // ۲.۶۷ (روزِ ۱۰ساعته) + ۰.۶۷ (روزِ ۸ساعته)
        Assert.Equal(5m, m.HolidayHours);
        Assert.Equal(23m, m.WorkedHours);        // ۱۰+۸+۵
    }

    [Fact]
    public void Monthly_Feeds_Payroll_Input()
    {
        // قراردادِ پل: خروجیِ تجمیع مستقیماً به FullPayrollInput می‌رود.
        var m = AttendanceCalculator.AggregateMonth(new[]
        {
            new DayAttendance(new TimeOnly(8, 0), new TimeOnly(18, 0)),
        }, Rules);

        var pay = PayrollCalculator.ComputeFull(new FullPayrollInput(
            BaseSalary: 100_000_000,
            OvertimeHours: m.OvertimeHours,
            NightHours: m.NightHours,
            HolidayHours: m.HolidayHours));

        Assert.True(pay.OvertimePay > 0);
        Assert.True(pay.Net > 0);
    }
}
