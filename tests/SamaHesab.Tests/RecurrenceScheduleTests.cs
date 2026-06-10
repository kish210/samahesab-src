using SamaHesab.Application.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>زمان‌بندی اسناد تکرارشونده — منطق خالص روی تاریخ شمسی.</summary>
public class RecurrenceScheduleTests
{
    [Theory]
    [InlineData("1404/01/15", "1404/02/15")]   // ماه بعد
    [InlineData("1404/12/10", "1405/01/10")]   // سرریز سال
    [InlineData("1404/01/31", "1404/02/29")]   // محدودسازی روز به ۲۹
    public void Monthly_Advances_One_Month(string from, string expected)
        => Assert.Equal(expected, RecurrenceSchedule.NextAfter(from, RecurrenceFrequency.Monthly));

    [Theory]
    [InlineData("1404/05/20", "1405/05/20")]
    [InlineData("1404/12/30", "1405/12/29")]   // روز ۳۰ → ۲۹
    public void Yearly_Advances_One_Year(string from, string expected)
        => Assert.Equal(expected, RecurrenceSchedule.NextAfter(from, RecurrenceFrequency.Yearly));

    [Theory]
    [InlineData("1404/03/01", "1404/03/05", true)]   // سررسید گذشته → موعد رسیده
    [InlineData("1404/03/05", "1404/03/05", true)]   // امروز = سررسید → موعد رسیده
    [InlineData("1404/04/01", "1404/03/05", false)]  // سررسید آینده → هنوز نه
    public void IsDue_Compares_Dates(string next, string today, bool expected)
        => Assert.Equal(expected, RecurrenceSchedule.IsDue(next, today));

    [Fact]
    public void Invalid_Date_Throws()
        => Assert.Throws<ArgumentException>(() => RecurrenceSchedule.NextAfter("bad", RecurrenceFrequency.Monthly));
}
