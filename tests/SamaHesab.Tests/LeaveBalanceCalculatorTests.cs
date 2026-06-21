using SamaHesab.Application.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ATT-C2-2 — موتورِ خالصِ ماندهٔ مرخصی.</summary>
public class LeaveBalanceCalculatorTests
{
    [Fact]
    public void MonthlyAccrual_Is_Annual_Over_Twelve()
    {
        Assert.Equal(2.17m, LeaveBalanceCalculator.MonthlyAccrual()); // ۲۶ ÷ ۱۲
    }

    [Fact]
    public void FullYear_Entitlement_Equals_Annual()
    {
        var b = LeaveBalanceCalculator.Compute(12, new LeaveUsage());
        Assert.Equal(26m, b.EntitlementDays);
        Assert.Equal(26m, b.RemainingDays);
    }

    [Fact]
    public void Usage_Reduces_Remaining()
    {
        // تا پایانِ ماهِ ۶: استحقاق ۱۳ روز ؛ مصرفِ ۳ روز → ماندهٔ ۱۰
        var b = LeaveBalanceCalculator.Compute(6, new LeaveUsage(UsedDays: 3));
        Assert.Equal(13m, b.EntitlementDays);
        Assert.Equal(10m, b.RemainingDays);
    }

    [Fact]
    public void Hourly_Leave_Converts_To_Days()
    {
        // ۷.۳۳ ساعت = ۱ روز
        var b = LeaveBalanceCalculator.Compute(12, new LeaveUsage(UsedHours: 7.33m));
        Assert.Equal(1m, b.UsedDays);
        Assert.Equal(25m, b.RemainingDays);
    }

    [Fact]
    public void CarryOver_Is_Capped()
    {
        // انتقالیِ ۲۰ روز با سقفِ ۹ → فقط ۹ اضافه می‌شود
        var b = LeaveBalanceCalculator.Compute(12, new LeaveUsage(), carryOverDays: 20);
        Assert.Equal(35m, b.EntitlementDays);  // ۲۶ + ۹
    }

    [Fact]
    public void CanRequest_Respects_Balance()
    {
        var b = LeaveBalanceCalculator.Compute(6, new LeaveUsage(UsedDays: 3)); // مانده ۱۰
        Assert.True(LeaveBalanceCalculator.CanRequest(b, 10));
        Assert.False(LeaveBalanceCalculator.CanRequest(b, 11));
        Assert.False(LeaveBalanceCalculator.CanRequest(b, 0));
    }

    [Fact]
    public void RemainingHours_Tracks_RemainingDays()
    {
        var b = LeaveBalanceCalculator.Compute(12, new LeaveUsage(UsedDays: 1));
        Assert.Equal(25m, b.RemainingDays);
        Assert.Equal(183.25m, b.RemainingHours); // ۲۵ × ۷.۳۳
    }
}
