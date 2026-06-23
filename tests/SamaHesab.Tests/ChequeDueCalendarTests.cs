using System.Linq;
using SamaHesab.Application.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>تقویمِ سررسیدِ چک — سطل‌بندیِ زمانی + ریزِ روزانه + جمعِ دریافتی/پرداختی.</summary>
public class ChequeDueCalendarTests
{
    private static ChequeDueBucket B(ChequeDueCalendarResult r, string key) => r.Buckets.Single(b => b.Key == key);

    [Fact]
    public void Buckets_Cheques_By_Distance_To_Today()
    {
        var today = "1405/03/10";
        var data = new[]
        {
            new ChequeDueInput("1405/03/05", 100, IsReceived: true),  // سررسیدگذشته
            new ChequeDueInput("1405/03/10", 200, IsReceived: true),  // امروز
            new ChequeDueInput("1405/03/15", 300, IsReceived: false), // ۵ روز → هفته
            new ChequeDueInput("1405/04/05", 400, IsReceived: false), // ~۲۶ روز → ماه
            new ChequeDueInput("1405/06/01", 500, IsReceived: true),  // بعدتر
        };

        var r = ChequeDueCalendar.Build(data, today);

        Assert.Equal(1, B(r, "overdue").ReceivedCount);
        Assert.Equal(100, B(r, "overdue").ReceivedAmount);
        Assert.Equal(200, B(r, "today").ReceivedAmount);
        Assert.Equal(1, B(r, "week").PaidCount);
        Assert.Equal(300, B(r, "week").PaidAmount);
        Assert.Equal(400, B(r, "month").PaidAmount);
        Assert.Equal(500, B(r, "later").ReceivedAmount);
    }

    [Fact]
    public void Totals_And_Net_Are_Correct()
    {
        var r = ChequeDueCalendar.Build(new[]
        {
            new ChequeDueInput("1405/03/15", 1000, IsReceived: true),
            new ChequeDueInput("1405/03/16", 600, IsReceived: false),
        }, "1405/03/10");

        Assert.Equal(1000, r.TotalReceived);
        Assert.Equal(600, r.TotalPaid);
        Assert.Equal(400, r.Net);
        Assert.Equal(400, B(r, "week").Net);   // 1000 received − 600 paid در همان سطل
    }

    [Fact]
    public void Day_Breakdown_Aggregates_Same_Date_And_Sorts()
    {
        var r = ChequeDueCalendar.Build(new[]
        {
            new ChequeDueInput("1405/03/15", 100, IsReceived: true),
            new ChequeDueInput("1405/03/15", 50, IsReceived: true),   // همان روز → جمع
            new ChequeDueInput("1405/03/12", 70, IsReceived: false),
        }, "1405/03/10");

        Assert.Equal(2, r.Days.Count);
        Assert.Equal("1405/03/12", r.Days[0].DueDate);  // مرتب صعودی
        var d15 = r.Days.Single(d => d.DueDate == "1405/03/15");
        Assert.Equal(2, d15.ReceivedCount);
        Assert.Equal(150, d15.ReceivedAmount);
        Assert.Equal(ChequeDueState.Upcoming, d15.State);
    }

    [Fact]
    public void Year_Boundary_Counts_As_Overdue_And_Upcoming()
    {
        var r = ChequeDueCalendar.Build(new[]
        {
            new ChequeDueInput("1404/12/29", 100, IsReceived: true),  // پارسال → گذشته
            new ChequeDueInput("1405/01/03", 200, IsReceived: true),  // ۲ روز بعد → هفته
        }, "1405/01/01");

        Assert.Equal(100, B(r, "overdue").ReceivedAmount);
        Assert.Equal(200, B(r, "week").ReceivedAmount);
    }

    [Fact]
    public void Invalid_Or_Missing_Today_Pushes_To_Later_Without_Crash()
    {
        var r = ChequeDueCalendar.Build(new[]
        {
            new ChequeDueInput("1405/03/15", 100, IsReceived: true),
        }, today: "");

        Assert.Equal(100, B(r, "later").ReceivedAmount);  // بدونِ امروزِ معتبر، محافظه‌کارانه «بعدتر»
        Assert.Equal(100, r.TotalReceived);
    }
}
