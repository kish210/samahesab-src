using System;
using System.Collections.Generic;
using System.Linq;
using SamaHesab.Application.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>نمای تقویمیِ ماهانهٔ حضوروغیاب — شبکهٔ روز×پرسنل + مغایرت (غیبت/تأخیر).</summary>
public class MonthlyAttendanceCalendarTests
{
    private static readonly AttendanceEmployeeRef[] TwoEmps =
    {
        new(1, "علی"), new(2, "رضا")
    };

    [Fact]
    public void Grid_Has_One_Cell_Per_Day_Per_Employee()
    {
        var r = MonthlyAttendanceCalendar.Build(1405, 1, TwoEmps, Array.Empty<AttendanceInput>());
        Assert.Equal(31, r.DaysInMonth);            // فروردین = ۳۱ روز
        Assert.Equal(2, r.Rows.Count);
        Assert.All(r.Rows, row => Assert.Equal(31, row.Cells.Count));
    }

    [Fact]
    public void Classifies_Present_Absent_Leave()
    {
        var recs = new[]
        {
            new AttendanceInput(1, "1405/01/01", "حاضر", null),
            new AttendanceInput(1, "1405/01/02", "غایب", null),
            new AttendanceInput(1, "1405/01/03", "مرخصی", null),
        };
        var r = MonthlyAttendanceCalendar.Build(1405, 1, TwoEmps, recs);
        var ali = r.Rows.Single(x => x.EmployeeId == 1);

        Assert.Equal(AttendanceCellState.Present, ali.Cells[0].State);
        Assert.Equal(AttendanceCellState.Absent, ali.Cells[1].State);
        Assert.Equal(AttendanceCellState.Leave, ali.Cells[2].State);
        Assert.Equal(1, ali.PresentDays);
        Assert.Equal(1, ali.AbsentDays);
        Assert.Equal(1, ali.LeaveDays);
    }

    [Fact]
    public void Late_Detected_When_CheckIn_After_Threshold()
    {
        var recs = new[]
        {
            new AttendanceInput(1, "1405/01/01", "حاضر", new TimeOnly(8, 30)),  // بعد از آستانه → تأخیر
            new AttendanceInput(1, "1405/01/02", "حاضر", new TimeOnly(7, 50)),  // به‌موقع
        };
        var r = MonthlyAttendanceCalendar.Build(1405, 1, TwoEmps, recs, lateThreshold: new TimeOnly(8, 0));
        var ali = r.Rows.Single(x => x.EmployeeId == 1);

        Assert.Equal(AttendanceCellState.Late, ali.Cells[0].State);
        Assert.Equal(AttendanceCellState.Present, ali.Cells[1].State);
        Assert.Equal(1, ali.LateDays);
        Assert.Equal(2, ali.PresentDays);   // تأخیر هم روزِ حاضر است
    }

    [Fact]
    public void Holiday_Cells_When_No_Record()
    {
        var r = MonthlyAttendanceCalendar.Build(1405, 1, TwoEmps, Array.Empty<AttendanceInput>(),
            holidays: new HashSet<int> { 13 });
        var ali = r.Rows.Single(x => x.EmployeeId == 1);

        Assert.Equal(AttendanceCellState.Holiday, ali.Cells[12].State);   // روز ۱۳
        Assert.Equal(AttendanceCellState.None, ali.Cells[0].State);
    }

    [Fact]
    public void Daily_Present_Counts_Sum_Across_Employees()
    {
        var recs = new[]
        {
            new AttendanceInput(1, "1405/01/01", "حاضر", null),
            new AttendanceInput(2, "1405/01/01", "حاضر", null),
            new AttendanceInput(1, "1405/01/02", "غایب", null),
        };
        var r = MonthlyAttendanceCalendar.Build(1405, 1, TwoEmps, recs);

        Assert.Equal(2, r.DailyPresentCounts[0]);   // روز ۱: هر دو حاضر
        Assert.Equal(0, r.DailyPresentCounts[1]);   // روز ۲: یکی غایب، دیگری بی‌رکورد
    }

    [Fact]
    public void Records_Of_Other_Months_Are_Ignored()
    {
        var recs = new[] { new AttendanceInput(1, "1405/02/01", "حاضر", null) };  // اردیبهشت
        var r = MonthlyAttendanceCalendar.Build(1405, 1, TwoEmps, recs);
        Assert.Equal(0, r.Rows.Single(x => x.EmployeeId == 1).PresentDays);
    }
}
