using System;
using SamaHesab.Domain.Entities.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ATT-C1-1 — اعتبارسنجی و چرخهٔ موجودیت‌های حضوروغیاب.</summary>
public class AttendanceEntitiesTests
{
    [Fact]
    public void Leave_Hourly_Requires_Hours()
    {
        Assert.Throws<ArgumentException>(() =>
            LeaveRequest.Create(1, 5, LeaveRequest.TypeHourly, "1404/01/10", "1404/01/10", days: 0, hours: 0));
    }

    [Fact]
    public void Leave_Daily_Requires_Days()
    {
        Assert.Throws<ArgumentException>(() =>
            LeaveRequest.Create(1, 5, LeaveRequest.TypeAnnual, "1404/01/10", "1404/01/12", days: 0));
    }

    [Fact]
    public void Leave_Hourly_Zeroes_Days_And_Keeps_Hours()
    {
        var l = LeaveRequest.Create(1, 5, LeaveRequest.TypeHourly, "1404/01/10", "", days: 3, hours: 4);
        Assert.Equal(0, l.Days);
        Assert.Equal(4, l.Hours);
        Assert.Equal("1404/01/10", l.EndDate);   // خالی → برابرِ شروع
        Assert.Equal(LeaveRequest.StatusPending, l.Status);
    }

    [Fact]
    public void Leave_Approve_Sets_Status_And_Blocks_Double_Decision()
    {
        var l = LeaveRequest.Create(1, 5, LeaveRequest.TypeAnnual, "1404/01/10", "1404/01/12", days: 3);
        l.Approve(99, "1404/01/09", "تأیید");
        Assert.Equal(LeaveRequest.StatusApproved, l.Status);
        Assert.Equal(99, l.DecidedBy);
        Assert.Throws<InvalidOperationException>(() => l.Reject(99, "1404/01/09"));
    }

    [Fact]
    public void Shift_Create_Validates_Name_And_Defaults()
    {
        Assert.Throws<ArgumentException>(() =>
            Shift.Create(1, "", new TimeOnly(8, 0), new TimeOnly(16, 0)));
        var s = Shift.Create(1, "اداری", new TimeOnly(8, 0), new TimeOnly(16, 0));
        Assert.True(s.IsActive);
        Assert.Equal(7.33m, s.StandardHours);
    }

    [Fact]
    public void Holiday_Defaults_Title_When_Empty()
    {
        var h = Holiday.Create(1, "1404/01/01", "");
        Assert.Equal("تعطیل", h.Title);
        Assert.True(h.IsOfficial);
    }
}
